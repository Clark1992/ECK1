using ECK1.CommandsAPI.Domain;
using ECK1.CommandsAPI.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace ECK1.CommandsAPI.Data;
public interface IRootRepository<TAggregate>
    where TAggregate : IAggregateRoot
{
    Task<TAggregate> LoadAsync(Guid aggregateId, CancellationToken ct);
    Task<List<Guid>> SaveAsync(TAggregate aggregate, CancellationToken ct);
}

internal class RootRepository<TAggregate, TEventEntity, TSnapshotEntity>(
    CommandsDbContext db, 
    IOptionsSnapshot<EventsStoreConfig> config) :
        IRootRepository<TAggregate>
        where TAggregate : class, IAggregateRootInternal, IAggregateRoot
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
            ? AggregateRoot.ReplayHistory(aggregate, domainEvents)
            : AggregateRoot.FromHistory<TAggregate>(domainEvents, aggregateId);

        loaded.InitUntouched();
        return loaded;
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
