using ECK1.FailedViewRebuilder.Data;
using ECK1.FailedViewRebuilder.Data.Models;
using ECK1.FailedViewRebuilder.Models;
using ECK1.Kafka;
using ECK1.Reconciliation.Contracts;
using Microsoft.EntityFrameworkCore;

namespace ECK1.FailedViewRebuilder.Services;

public interface IJobRunner
{
    Task RunJob<TOrder>(
        string entityType,
        QueryParams<TOrder> qParams,
        int jobId,
        CancellationToken ct);
}

public class JobRunner(
    IReadOnlyDictionary<string, IKafkaTopicProducer<RebuildRequest>> rebuildProducers,
    FailuresDbContext db,
    ILogger<JobRunner> logger) : IJobRunner
{
    protected readonly int BatchSize = 1000;

    public async Task RunJob<TKey>(
        string entityType,
        QueryParams<TKey> qParams,
        int jobId,
        CancellationToken ct)
    {
        var producer = rebuildProducers.GetValueOrDefault(entityType);
        if (producer is null)
        {
            logger.LogWarning("No rebuild producer registered for entity type '{EntityType}'. Skipping.", entityType);
            return;
        }

        JobHistory job = null;
        try
        {
            logger.LogInformation("Start sending rebuild view requests.");
            while (true)
            {
                job = await db.JobHistories.FindAsync([jobId], cancellationToken: ct);

                if (job is null)
                {
                    logger.LogInformation("Cant find job history record (jobId = {jobId}).", jobId);
                    return;
                }

                if (job.FinishedAt.HasValue)
                {
                    logger.LogInformation("Seems like job ({entityType}) has been forcefully stopped. Stopping sending messages...", entityType);
                    return;
                }

                IQueryable<EventFailure> failedEventsQuery = db.Set<EventFailure>();

                if (qParams.Filter is not null)
                {
                    failedEventsQuery = failedEventsQuery.Where(qParams.Filter);
                }

                failedEventsQuery = qParams.IsAsc ? failedEventsQuery.OrderBy(qParams.OrderBy) : failedEventsQuery.OrderByDescending(qParams.OrderBy);

                failedEventsQuery = failedEventsQuery.Take(qParams.Count ?? BatchSize);

                var failedEvents = await failedEventsQuery.ToListAsync(ct);

                logger.LogInformation("Retrieved {count} failures.", failedEvents.Count);

                if (failedEvents.Count == 0)
                    break;

                await Process(producer, entityType, failedEvents, ct);

                // run once for the whole count when count is set in params
                if (qParams.Count.HasValue)
                    break;
            }

            logger.LogInformation("Finished sending rebuild view requests.");
            job.IsSuccess = true;
        }
        catch (Exception ex)
        {
            logger.LogError("Error during sending rebuild view requests. EntityType: {entityType}. Exception: {ex}", entityType, ex);
            if (job is not null)
            {
                job.ErrorMessage = $"{ex.Message}\n{ex.StackTrace}";
                job.IsSuccess = false;
            }
        }
        finally
        {
            if (job is not null && !job.FinishedAt.HasValue)
            {
                job.FinishedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }
    }

    private async Task Process(
        IKafkaTopicProducer<RebuildRequest> producer,
        string entityType,
        List<EventFailure> failedEvents,
        CancellationToken ct)
    {
        foreach (var failure in failedEvents)
        {
            var request = new RebuildRequest
            {
                EntityId = failure.EntityId,
                EntityType = entityType,
                FailedTargets = [],
                IsFullHistoryRebuild = false,
            };

            await producer.ProduceAsync(request, failure.EntityId.ToString(), ct);
        }

        db.Set<EventFailure>().RemoveRange(failedEvents);
        await db.SaveChangesAsync(ct);
    }
}
