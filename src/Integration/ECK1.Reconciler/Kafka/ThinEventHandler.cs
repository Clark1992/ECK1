using ECK1.IntegrationContracts.Kafka.IntegrationRecords.Generated;
using ECK1.Reconciler.Data;

namespace ECK1.Reconciler.Kafka;

public class ThinEventHandler(
    ReconcilerRepository repository,
    ILogger<ThinEventHandler> logger)
{
    public async Task HandleAsync(string entityType, ThinEvent @event, CancellationToken ct)
    {
        logger.LogInformation(
            "ThinEvent received: EntityType={EntityType}, EntityId={EntityId}, Version={Version}",
            entityType, @event.EntityId, @event.Version);

        await repository.UpsertEntityStateAsync(
            @event.EntityId,
            entityType,
            @event.Version,
            @event.OccuredAt,
            ct);
    }
}
