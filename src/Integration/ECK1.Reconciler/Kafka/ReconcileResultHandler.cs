using ECK1.Reconciliation.Contracts;
using ECK1.Kafka;
using ECK1.Reconciler.Data;

namespace ECK1.Reconciler.Kafka;

public class ReconcileResultHandler(
    ReconcilerRepository repository,
    ILogger<ReconcileResultHandler> logger) : IKafkaMessageHandler<ReconcileResult>
{
    public async Task Handle(string key, ReconcileResult message, KafkaMessageId messageId, CancellationToken ct)
    {
        logger.LogWarning(
            "Reconcile failure received: EntityType={EntityType}, EntityId={EntityId}, FailedPlugin={FailedPlugin}, IsFullHistoryRebuild={IsFullHistoryRebuild}",
            message.EntityType, message.EntityId, message.FailedPlugin, message.IsFullHistoryRebuild);

        await repository.AddReconcileFailureAsync(
            message.EntityId,
            message.EntityType,
            message.FailedPlugin,
            message.IsFullHistoryRebuild,
            ct);
    }
}
