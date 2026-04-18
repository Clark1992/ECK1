using ECK1.CommonUtils.Chaos;
using ECK1.Kafka;
using ECK1.Reconciliation.Contracts;
using ECK1.Reconciler.Data;
using Microsoft.Extensions.Options;

namespace ECK1.Reconciler.Services;

public class RebuildDispatchService(
    IServiceScopeFactory scopeFactory,
    IReadOnlyDictionary<string, IKafkaTopicProducer<RebuildRequest>> rebuildProducers,
    IChaosEngine chaosEngine,
    IOptions<ReconcilerSettings> options,
    ILogger<RebuildDispatchService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        var interval = TimeSpan.FromMinutes(settings.RebuildDispatchIntervalMinutes);

        logger.LogInformation(
            "RebuildDispatchService started. Interval: {Interval} minutes",
            settings.RebuildDispatchIntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(interval, stoppingToken);

            if (chaosEngine.IsActive(ChaosScenarios.Reconciler.PauseDispatching))
            {
                logger.LogWarning("CHAOS: Rebuild dispatching paused by '{Scenario}'", ChaosScenarios.Reconciler.PauseDispatching);
                continue;
            }

            try
            {
                await DispatchRebuildsAsync(settings.RebuildBatchSize, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during rebuild dispatch");
            }
        }
    }

    private async Task DispatchRebuildsAsync(int batchSize, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ReconcilerRepository>();

        var failures = await repository.GetUndispatchedFailuresAsync(batchSize, ct);

        if (failures.Count == 0)
        {
            logger.LogDebug("No undispatched reconcile failures");
            return;
        }

        // Group by entity + rebuild type to send separate messages for full history vs latest
        var grouped = failures
            .GroupBy(f => new { f.EntityId, f.EntityType, f.IsFullHistoryRebuild });

        foreach (var group in grouped)
        {
            var request = new RebuildRequest
            {
                EntityId = group.Key.EntityId,
                EntityType = group.Key.EntityType,
                IsFullHistoryRebuild = group.Key.IsFullHistoryRebuild,
                FailedTargets = [.. group.Select(f => f.FailedPlugin).Distinct()]
            };

            var producer = rebuildProducers.GetValueOrDefault(request.EntityType);
            if (producer is null)
            {
                logger.LogWarning("No rebuild producer registered for entity type '{EntityType}'. Skipping.", request.EntityType);
                continue;
            }

            logger.LogInformation(
                "Dispatching rebuild request: EntityType={EntityType}, EntityId={EntityId}, IsFullHistoryRebuild={IsFullHistory}, Targets=[{Targets}]",
                request.EntityType, request.EntityId, request.IsFullHistoryRebuild,
                string.Join(", ", request.FailedTargets));

            await producer.ProduceAsync(request, request.EntityId.ToString(), ct);
        }

        await repository.MarkFailuresDispatchedAsync([.. failures.Select(f => f.Id)], ct);
    }
}
