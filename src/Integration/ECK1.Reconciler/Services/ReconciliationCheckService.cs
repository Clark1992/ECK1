using AutoMapper;
using ECK1.Reconciliation.Contracts;
using ECK1.Kafka;
using ECK1.Reconciler.Data;
using Microsoft.Extensions.Options;

namespace ECK1.Reconciler.Services;

public class ReconciliationCheckService(
    IServiceScopeFactory scopeFactory,
    IKafkaTopicProducer<ReconcileRequest> producer,
    IMapper mapper,
    IOptions<ReconcilerSettings> options,
    ILogger<ReconciliationCheckService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        var interval = TimeSpan.FromMinutes(settings.ReconciliationCheckIntervalMinutes);

        logger.LogInformation(
            "ReconciliationCheckService started. Interval: {Interval} minutes",
            settings.ReconciliationCheckIntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(interval, stoppingToken);

            try
            {
                await RunCheckAsync(settings.ReconcileBatchSize, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during reconciliation check");
            }
        }
    }

    private async Task RunCheckAsync(int batchSize, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ReconcilerRepository>();

        var entities = await repository.GetUnreconciledEntitiesAsync(batchSize, ct);

        if (entities.Count == 0)
        {
            logger.LogDebug("No unreconciled entities found");
            return;
        }

        logger.LogInformation("Sending reconcile request for {Count} entities", entities.Count);

        var request = new ReconcileRequest
        {
            Items = mapper.Map<List<ReconcileRequestItem>>(entities)
        };

        await producer.ProduceAsync(request, Guid.NewGuid().ToString(), ct);

        foreach (var entity in entities)
        {
            await repository.MarkReconciledAsync(entity.EntityId, entity.EntityType, ct);
        }
    }
}
