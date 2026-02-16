using ECK1.FailedViewRebuilder.Data;
using ECK1.FailedViewRebuilder.Data.Models;
using ECK1.FailedViewRebuilder.Models;
using ECK1.Kafka;
using Microsoft.EntityFrameworkCore;

namespace ECK1.FailedViewRebuilder.Services;

public interface IJobRunner
{
    Task RunJob<TOrder>(
        string topic,
        QueryParams<TOrder> qParams,
        int jobId,
        CancellationToken ct);
}

public class JobRunner(
    IKafkaSimpleProducer<Guid> producer,
    FailuresDbContext db,
    ILogger<JobRunner> logger) : IJobRunner
{
    protected readonly int BatchSize = 1000;

    public async Task RunJob<TKey>(
        string topic,
        QueryParams<TKey> qParams,
        int jobId,
        CancellationToken ct)
    {
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
                    logger.LogInformation("Seems like job ({topic}) has been forcefully stopped. Stopping sending messages...", topic);
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

                await Process(topic, failedEvents);

                // run once for the whole count when count is set in params
                if (qParams.Count.HasValue)
                    break;
            }

            logger.LogInformation("Finished sending rebuild view requests.");
            job.IsSuccess = true;
        }
        catch (Exception ex)
        {
            logger.LogError("Error during sending rebuild view requests. Job key (topic): {topic}. Exception: {ex}", topic, ex);
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

    private async Task Process(string topic, List<EventFailure> failedEvents)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        failedEvents.ForEach(e => producer.ProduceAsync(e.EntityId, topic, default));
        db.Set<EventFailure>().RemoveRange(failedEvents);
        await db.SaveChangesAsync();
    }
}
