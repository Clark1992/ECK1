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

public class RootRepository<TAggregate, TEventEntity, TSnapshotEntity> :
    IRootRepository<TAggregate>
    where TAggregate : class, IAggregateRoot
    where TEventEntity : class, IEventEntity
    where TSnapshotEntity : SnapshotEntity, new()
{
    private readonly int _snapshotInterval;

    public RootRepository(CommandsDbContext db, IOptionsSnapshot<EventsStoreConfig> config)
    {
        Db = db;
        _snapshotInterval = config.Value.SnapshotInterval;
    }

    protected CommandsDbContext Db { get; }

    public async Task<List<Guid>> SaveAsync(TAggregate aggregate, CancellationToken ct)
    {
        var currentVersion = await GetCurrentVersionAsync(aggregate.Id, ct);
        OptimisticEventStoreSaveHelper.ThrowIfUnexpectedVersion(
            aggregate,
            currentVersion);

        var version = currentVersion;
        List<TEventEntity> persistedEntities = [];

        foreach (var domainEvent in aggregate.UncommittedEvents)
        {
            version++;

            var eventEntity = TEventEntity.FromDomainEvent(domainEvent, version) is TEventEntity e ?
                                e :
                                throw new InvalidOperationException("Wrong event entity type.");

            Db.Set<TEventEntity>().Add(eventEntity);
            persistedEntities.Add(eventEntity);
        }

        try
        {
            await Db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (OptimisticEventStoreSaveHelper.IsDuplicateVersionConflict(ex))
        {
            Db.ChangeTracker.Clear();
            throw OptimisticEventStoreSaveHelper.CreateConflictException(aggregate, currentVersion, "append");
        }

        List<Guid> eventIds = [.. persistedEntities.Select(e => e.EventId)];

        aggregate.CommitEvents(version);

        if (_snapshotInterval > 0 && version % _snapshotInterval == 0)
        {
            await SaveSnapshotAsync(aggregate, ct);
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

        var events = await Db.Set<TEventEntity>()
            .Where(e => e.AggregateId == aggregateId && e.Version > snapshotVersion)
            .OrderBy(e => e.Version)
            .ToListAsync(ct);

        var domainEvents = events.Select(e => e.ToDomainEvent()).ToList();

        if (aggregate is null && domainEvents.Count == 0)
        {
            return null;
        }

        return aggregate is not null
            ? AggregateRoot.ReplayHistory(aggregate, domainEvents)
            : AggregateRoot.FromHistory<TAggregate>(domainEvents, aggregateId);
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

    protected virtual async Task<int> GetCurrentVersionAsync(Guid aggregateId, CancellationToken ct)
    {
        var version = await Db.Set<TEventEntity>()
            .Where(e => e.AggregateId == aggregateId)
            .OrderByDescending(e => e.Version)
            .Select(e => (int?)e.Version)
            .FirstOrDefaultAsync(ct);

        return version ?? 0;
    }

    protected virtual TAggregate DeserializeSnapshot(TSnapshotEntity snapshotEntity) =>
        JsonSerializer.Deserialize<TAggregate>(snapshotEntity.SnapshotData)
           ?? throw new InvalidOperationException("Failed to deserialize snapshot");
}
