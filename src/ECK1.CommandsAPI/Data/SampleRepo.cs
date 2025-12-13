using ECK1.CommandsAPI.Data.Models;
using ECK1.CommandsAPI.Domain.Samples;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace ECK1.CommandsAPI.Data;

public interface ISampleRepo
{
    Task<Sample> LoadAsync(Guid sampleId, CancellationToken ct);
    Task<List<Guid>> SaveAsync(Sample aggregate, CancellationToken ct);
}

public class SampleRepo : ISampleRepo
{
    private readonly CommandsDbContext _db;
    private readonly int _snapshotInterval;

    public SampleRepo(CommandsDbContext db, IOptionsSnapshot<EventsStoreConfig> config)
    {
        _db = db;
        _snapshotInterval = config.Value.SnapshotInterval;
    }

    public async Task<List<Guid>> SaveAsync(Sample aggregate, CancellationToken ct)
    {
        var lastVersion = await _db.SampleEvents
            .Where(e => e.SampleId == aggregate.Id)
            .MaxAsync(e => (int?)e.Version, ct) ?? 0;

        int version = lastVersion;

        var newEvents = new List<SampleEventEntity>();

        foreach (var ev in aggregate.UncommittedEvents)
        {
            version++;
            var eventEntity = SampleEventEntity.FromDomainEvent(ev, version);
            _db.SampleEvents.Add(eventEntity);
            newEvents.Add(eventEntity);
        }

        await _db.SaveChangesAsync(ct);

        var newEventIds = newEvents.Select(e => e.EventId).ToList();

        aggregate.MarkEventsAsCommitted(version);

        // Auto snapshot creation
        if (_snapshotInterval > 0 && version % _snapshotInterval == 0)
        {
            await SaveSnapshotAsync(aggregate, ct);
        }

        return newEventIds;
    }

    public async Task<Sample> LoadAsync(Guid sampleId, CancellationToken ct)
    {
        var snapshotEntity = await _db.SampleSnapshots
            .Where(s => s.SampleId == sampleId)
            .OrderByDescending(s => s.Version)
            .FirstOrDefaultAsync(ct);

        Sample sample = null;
        int snapshotVersion = 0;

        if (snapshotEntity != null)
        {
            sample = JsonSerializer.Deserialize<Sample>(snapshotEntity.SnapshotData)
                     ?? throw new InvalidOperationException("Failed to deserialize snapshot");
            snapshotVersion = snapshotEntity.Version;
        }

        var events = await _db.SampleEvents
            .Where(e => e.SampleId == sampleId && e.Version > snapshotVersion)
            .OrderBy(e => e.Version)
            .ToListAsync(ct);

        var domainEvents = events.Select(e => e.ToDomainEvent()).ToList();
        var latestSample = sample?.ReplayHistory<Sample>(domainEvents) ?? Sample.FromHistory<Sample>(domainEvents, sampleId);

        return latestSample;
    }

    private async Task SaveSnapshotAsync(Sample aggregate, CancellationToken ct)
    {
        var snapshot = new SampleSnapshotEntity
        {
            SnapshotId = Guid.NewGuid(),
            SampleId = aggregate.Id,
            Version = aggregate.Version,
            SnapshotData = JsonSerializer.Serialize(aggregate),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.SampleSnapshots.Add(snapshot);
        await _db.SaveChangesAsync(ct);
    }
}
