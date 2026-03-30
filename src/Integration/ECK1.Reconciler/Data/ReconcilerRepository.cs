using ECK1.Reconciler.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace ECK1.Reconciler.Data;

public class ReconcilerRepository(ReconcilerDbContext db)
{
    public async Task UpsertEntityStateAsync(
        Guid entityId,
        string entityType,
        int version,
        DateTime occuredAt,
        CancellationToken ct)
    {
        var existing = await db.EntityStates
            .FirstOrDefaultAsync(x => x.EntityId == entityId && x.EntityType == entityType, ct);

        if (existing is null)
        {
            db.EntityStates.Add(new Models.EntityState
            {
                EntityId = entityId,
                EntityType = entityType,
                ExpectedVersion = version,
                LastEventOccuredAt = occuredAt,
                ReconciledAt = null
            });
        }
        else
        {
            if (version > existing.ExpectedVersion)
            {
                existing.ExpectedVersion = version;
                existing.LastEventOccuredAt = occuredAt;
            }

            existing.ReconciledAt = null;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<List<Models.EntityState>> GetUnreconciledEntitiesAsync(
        int batchSize,
        CancellationToken ct)
    {
        return await db.EntityStates
            .Where(x => x.ReconciledAt == null)
            .OrderBy(x => x.LastEventOccuredAt)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public async Task MarkReconciledAsync(
        Guid entityId,
        string entityType,
        CancellationToken ct)
    {
        await db.EntityStates
            .Where(x => x.EntityId == entityId && x.EntityType == entityType)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ReconciledAt, DateTime.UtcNow), ct);
    }

    public async Task AddReconcileFailureAsync(
        Guid entityId,
        string entityType,
        string failedPlugin,
        bool isFullHistoryRebuild,
        CancellationToken ct)
    {
        db.ReconcileFailures.Add(new ReconcileFailure
        {
            EntityId = entityId,
            EntityType = entityType,
            FailedPlugin = failedPlugin,
            IsFullHistoryRebuild = isFullHistoryRebuild,
            FailedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
    }

    public async Task<List<ReconcileFailure>> GetUndispatchedFailuresAsync(
        int batchSize,
        CancellationToken ct)
    {
        return await db.ReconcileFailures
            .Where(x => x.DispatchedAt == null)
            .OrderBy(x => x.FailedAt)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public async Task MarkFailuresDispatchedAsync(
        IEnumerable<int> failureIds,
        CancellationToken ct)
    {
        await db.ReconcileFailures
            .Where(x => failureIds.Contains(x.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.DispatchedAt, DateTime.UtcNow), ct);
    }
}
