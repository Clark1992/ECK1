using Confluent.Kafka;
using ECK1.CommandsAPI.Data.Models;
using ECK1.CommandsAPI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace ECK1.CommandsAPI.Data;
public interface IRootRepository<TAggregate>
    where TAggregate : IAggregateRootInternal
{
    Task<TAggregate> LoadAsync(Guid aggregateId, CancellationToken ct);
    Task<List<IDomainEvent>> LoadHistory(Guid aggregateId, CancellationToken ct);
    Task<IDomainEvent> GetLatestEvent(Guid rootId, CancellationToken ct);
    Task<List<Guid>> SaveAsync(TAggregate aggregate, CancellationToken ct);
}

internal class RootRepository<TAggregate, TEventEntity, TSnapshotEntity>(
    CommandsDbContext db, 
    IOptionsSnapshot<EventsStoreConfig> config) :
        IRootRepository<TAggregate>
        where TAggregate : class, IAggregateRoot, IAggregateRootInternal
        where TEventEntity : class, IEventEntity
        where TSnapshotEntity : SnapshotEntity, new()
{
    private readonly int _snapshotInterval = config.Value.SnapshotInterval;

    protected CommandsDbContext Db { get; } = db;

    public async Task<List<Guid>> SaveAsync(TAggregate aggregate, CancellationToken ct)
    {
        List<TEventEntity> persistedEntities = [];

        foreach (var domainEvent in aggregate.UncommittedEvents)
        {
            var eventEntity = TEventEntity.FromDomainEvent(domainEvent) is TEventEntity e ?
                                e :
                                throw new InvalidOperationException("Wrong event entity type.");

            Db.Set<TEventEntity>().Add(eventEntity);
            persistedEntities.Add(eventEntity);
        }

        try
        {
            await Db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsDuplicateVersionConflict(ex))
        {
            Db.ChangeTracker.Clear();
            throw new ConcurrencyConflictException(aggregate, ex.Message);
        }

        List<Guid> eventIds = [.. persistedEntities.Select(e => e.EventId)];

        if (_snapshotInterval > 0)
        {
            var initialVersion = aggregate.Untouched.Version;
            var versionsCountToLeftToSnapshot = _snapshotInterval - initialVersion % _snapshotInterval;
            var shouldBuildSnapshot = persistedEntities.Count > versionsCountToLeftToSnapshot;

            if (shouldBuildSnapshot)
            {
                await SaveSnapshotAsync(aggregate, ct);
            }
        }

        return eventIds;
    }

    public async Task<TAggregate> LoadAsync(Guid aggregateId, CancellationToken ct)
    {
        var snapshotEntity = await Db.Set<TSnapshotEntity>()
            .Where(s => s.AggregateId == aggregateId)
            .OrderByDescending(s => s.Version)
            .FirstOrDefaultAsync(ct);

        TAggregate aggregate = null;
        var snapshotVersion = 0;

        if (snapshotEntity is not null)
        {
            aggregate = DeserializeSnapshot(snapshotEntity);
            snapshotVersion = snapshotEntity.Version;
        }

        var domainEvents = await Db.Set<TEventEntity>()
            .Where(e => e.AggregateId == aggregateId && e.Version > snapshotVersion)
            .OrderBy(e => e.Version)
            .Select(e => e.ToDomainEvent())
            .ToListAsync(ct);

        if (aggregate is null && domainEvents.Count == 0)
        {
            return null;
        }

        var loaded = aggregate is not null
            ? AggregateRoot.FromSnapshot(aggregate, domainEvents)
            : AggregateRoot.FromStart<TAggregate>(domainEvents, aggregateId);

        return loaded;
    }

    public async Task<IDomainEvent> GetLatestEvent(Guid rootId, CancellationToken ct)
    {
        var events = await LoadHistory(rootId, 1, ct);

        return events.Count > 0 ? events[0] : null;
    }

    public Task<List<IDomainEvent>> LoadHistory(Guid rootId, CancellationToken ct) =>
        LoadHistory(rootId, null, ct);

    private async Task<List<IDomainEvent>> LoadHistory(Guid aggregateId, int? takeLast, CancellationToken ct)
    {
        var query = Db.Set<TEventEntity>()
            .Where(e => e.AggregateId == aggregateId)
            .OrderBy(e => e.Version)
            .Select(e => e.ToDomainEvent());

        if (takeLast.HasValue)
        {
            query = query.TakeLast(takeLast.Value);
        }

        return await query.ToListAsync(ct);
    }

    private async Task SaveSnapshotAsync(TAggregate aggregate, CancellationToken ct)
    {
        var snapshotEntity = new TSnapshotEntity
        {
            SnapshotId = Guid.NewGuid(),
            AggregateId = aggregate.Id,
            Version = aggregate.Version,
            SnapshotData = JsonSerializer.Serialize(aggregate),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        Db.Set<TSnapshotEntity>().Add(snapshotEntity);
        await Db.SaveChangesAsync(ct);
    }

    protected virtual TAggregate DeserializeSnapshot(TSnapshotEntity snapshotEntity) =>
        JsonSerializer.Deserialize<TAggregate>(snapshotEntity.SnapshotData)
           ?? throw new InvalidOperationException("Failed to deserialize snapshot");

    private static bool IsDuplicateVersionConflict(DbUpdateException ex)
    {
        if (ex.InnerException is not Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            return false;
        }

        return sqlEx.Number is 2601 or 2627;
    }
}
