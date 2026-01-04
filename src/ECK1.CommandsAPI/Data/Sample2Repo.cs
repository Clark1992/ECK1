using ECK1.CommandsAPI.Data.Models;
using ECK1.CommandsAPI.Domain.Sample2s;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace ECK1.CommandsAPI.Data;

public interface ISample2Repo
{
    Task<Sample2> LoadAsync(Guid sample2Id, CancellationToken ct);
    Task<List<Guid>> SaveAsync(Sample2 aggregate, CancellationToken ct);
}

public class Sample2Repo : ISample2Repo
{
    private readonly CommandsDbContext _db;
    private readonly int _snapshotInterval;

    public Sample2Repo(CommandsDbContext db, IOptionsSnapshot<EventsStoreConfig> config)
    {
        _db = db;
        _snapshotInterval = config.Value.SnapshotInterval;
    }

    public async Task<List<Guid>> SaveAsync(Sample2 aggregate, CancellationToken ct)
    {
        var lastVersion = await _db.Sample2Events
            .Where(e => e.Sample2Id == aggregate.Id)
            .MaxAsync(e => (int?)e.Version, ct) ?? 0;

        int version = lastVersion;

        var newEvents = new List<Sample2EventEntity>();

        foreach (var ev in aggregate.UncommittedEvents)
        {
            version++;
            var eventEntity = Sample2EventEntity.FromDomainEvent(ev, version);
            _db.Sample2Events.Add(eventEntity);
            newEvents.Add(eventEntity);
        }

        await _db.SaveChangesAsync(ct);

        var newEventIds = newEvents.Select(e => e.EventId).ToList();

        aggregate.CommitEvents(version);

        if (_snapshotInterval > 0 && version % _snapshotInterval == 0)
        {
            await SaveSnapshotAsync(aggregate, ct);
        }

        return newEventIds;
    }

    public async Task<Sample2> LoadAsync(Guid sample2Id, CancellationToken ct)
    {
        var snapshotEntity = await _db.Sample2Snapshots
            .Where(s => s.Sample2Id == sample2Id)
            .OrderByDescending(s => s.Version)
            .FirstOrDefaultAsync(ct);

        Sample2 sample2 = null;
        int snapshotVersion = 0;

        if (snapshotEntity != null)
        {
            sample2 = JsonSerializer.Deserialize<Sample2>(snapshotEntity.SnapshotData)
                     ?? throw new InvalidOperationException("Failed to deserialize snapshot");
            snapshotVersion = snapshotEntity.Version;
        }

        var events = await _db.Sample2Events
            .Where(e => e.Sample2Id == sample2Id && e.Version > snapshotVersion)
            .OrderBy(e => e.Version)
            .ToListAsync(ct);

        var domainEvents = events.Select(e => e.ToDomainEvent()).ToList();
        var latestSample2 = sample2?.ReplayHistory<Sample2>(domainEvents) ?? Sample2.FromHistory<Sample2>(domainEvents, sample2Id);

        return latestSample2;
    }

    private async Task SaveSnapshotAsync(Sample2 aggregate, CancellationToken ct)
    {
        var snapshot = new Sample2SnapshotEntity
        {
            SnapshotId = Guid.NewGuid(),
            Sample2Id = aggregate.Id,
            Version = aggregate.Version,
            SnapshotData = JsonSerializer.Serialize(aggregate),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Sample2Snapshots.Add(snapshot);
        await _db.SaveChangesAsync(ct);
    }
}
