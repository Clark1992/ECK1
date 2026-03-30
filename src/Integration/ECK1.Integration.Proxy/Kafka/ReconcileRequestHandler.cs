using ECK1.Reconciliation.Contracts;
using ECK1.Integration.Plugin.Abstractions;
using ECK1.Integration.Config;
using ECK1.Kafka;

namespace ECK1.Integration.Proxy.Kafka;

public class ReconcileRequestHandler(
    IReconciliationPlugin reconciliationPlugin,
    IKafkaTopicProducer<ReconcileResult> resultProducer,
    IntegrationConfig integrationConfig,
    ILogger<ReconcileRequestHandler> logger) : IKafkaMessageHandler<ReconcileRequest>
{
    private readonly string pluginName = integrationConfig.Values
        .Select(e => e.PluginConfig?.Key)
        .FirstOrDefault(k => k is not null) ?? "Unknown";

    public async Task Handle(string key, ReconcileRequest message, KafkaMessageId messageId, CancellationToken ct)
    {
        foreach (var item in message.Items)
        {
            try
            {
                var result = await reconciliationPlugin.CheckAsync(item.EntityId, item.EntityType, item.ExpectedVersion, ct);

                if (!result.IsConsistent)
                {
                    logger.LogWarning(
                        "Reconciliation mismatch for {EntityType}:{EntityId} in plugin {Plugin}",
                        item.EntityType, item.EntityId, pluginName);

                    var reconcileResult = new ReconcileResult
                    {
                        EntityId = item.EntityId,
                        EntityType = item.EntityType,
                        FailedPlugin = pluginName,
                        IsFullHistoryRebuild = result.RequiresFullHistoryRebuild
                    };

                    await resultProducer.ProduceAsync(reconcileResult, item.EntityId.ToString(), ct);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during reconciliation check for {EntityType}:{EntityId} in {Plugin}",
                    item.EntityType, item.EntityId, pluginName);
            }
        }
    }
}
