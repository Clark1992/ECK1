using ECK1.Kafka;
using ECK1.Reconciliation.Contracts;
using ECK1.VersionTracker.Contracts;
using ECK1.VersionTracker.Storage;

namespace ECK1.VersionTracker.Services;

public class VersionTrackerGrpcService(
    VersionStore store,
    IReadOnlyDictionary<string, IKafkaTopicProducer<RebuildRequest>> rebuildProducers,
    ILogger<VersionTrackerGrpcService> logger) : IVersionTrackerService
{
    public async ValueTask<PutVersionResponse> PutVersion(PutVersionRequest request)
    {
        await store.PutAsync(request.EntityType, request.EntityId, request.Version);
        logger.LogDebug("PutVersion: {EntityType}:{EntityId} → {Version}",
            request.EntityType, request.EntityId, request.Version);
        return new PutVersionResponse();
    }

    public async ValueTask<GetVersionResponse> GetVersion(GetVersionRequest request)
    {
        var stored = await store.GetAsync(request.EntityType, request.EntityId);

        if (request.ExpectedVersion > stored)
        {
            logger.LogInformation(
                "Self-healing: {EntityType}:{EntityId} stored={Stored}, expected={Expected}. Dispatching rebuild.",
                request.EntityType, request.EntityId, stored, request.ExpectedVersion);

            _ = DispatchRebuildAsync(request);
        }

        return new GetVersionResponse { Version = stored };
    }

    private async Task DispatchRebuildAsync(GetVersionRequest request)
    {
        try
        {
            if (!Guid.TryParse(request.EntityId, out var entityGuid))
            {
                logger.LogWarning("Cannot parse EntityId '{EntityId}' as Guid for rebuild", request.EntityId);
                return;
            }

            if (!rebuildProducers.TryGetValue(request.EntityType, out var producer))
            {
                logger.LogWarning("No rebuild producer registered for entity type '{EntityType}'", request.EntityType);
                return;
            }

            var rebuildRequest = new RebuildRequest
            {
                EntityId = entityGuid,
                EntityType = request.EntityType,
                FailedTargets = [VersionTrackerConstants.TargetName],
                IsFullHistoryRebuild = false
            };

            await producer.ProduceAsync(rebuildRequest, request.EntityId, CancellationToken.None);

            logger.LogInformation("Rebuild request dispatched for {EntityType}:{EntityId}",
                request.EntityType, request.EntityId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send rebuild request for {EntityType}:{EntityId}",
                request.EntityType, request.EntityId);
        }
    }
}
