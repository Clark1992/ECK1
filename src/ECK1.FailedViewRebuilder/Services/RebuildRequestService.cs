using ECK1.FailedViewRebuilder.Data;
using ECK1.FailedViewRebuilder.Data.Models;
using ECK1.Kafka;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace ECK1.FailedViewRebuilder.Services;

public class FailedViewsResponse<TId>
{
    public int Count { get; set; }
    public List<TId> TopIds { get; set; }
}

public interface IRebuildRequestService<TEntity, TMessage> where TEntity : class
{
    Task<string> StartJob<TKey>(string topic, Expression<Func<TEntity, TKey>> orderBy, bool isAsc, int? count, Func<TEntity, TMessage> valueMapper);

    Task<string> StopJob(string topic);

    Task<FailedViewsResponse<TId>> GetFailedViewsOverview<TId, TKey>(
        Func<TEntity, TId> idSelector,
        Expression<Func<TEntity, TKey>> orderBy,
        bool isAsc);
}

public class RebuildRequestService<TEntity, TMessage>(
    IKafkaSimpleProducer<TMessage> producer,
    FailuresDbContext db,
    ILogger<RebuildRequestService<TEntity, TMessage>> logger) : IRebuildRequestService<TEntity, TMessage> where TEntity : class
{
    protected readonly int BatchSize = 1000;

    public async Task<string> StartJob<TKey>(
        string topic,
        Expression<Func<TEntity, TKey>> orderBy,
        bool isAsc,
        int? count,
        Func<TEntity, TMessage> valueMapper)
    {
        var job = await db.JobHistories.FirstOrDefaultAsync(j => j.Name == topic && j.FinishedAt == null);

        if (job is not null)
        {
            logger.LogInformation("Previous job ({topic}) is still in progress", topic);

            return $"Previous job ({topic}) started at {job.StartedAt} is still in progress.";
        }

        job = new JobHistory
        {
            Name = topic,
            StartedAt = DateTimeOffset.UtcNow,
        };

        db.JobHistories.Add(job);

        await db.SaveChangesAsync();

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        Task.Run(() => ProduceRequests(topic, orderBy, isAsc, count, valueMapper, job.Id));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

        return $"Started {job.Name} at {job.StartedAt}";
    }

    private async Task ProduceRequests<TKey>(
        string topic,
        Expression<Func<TEntity, TKey>> orderBy,
        bool isAsc,
        int? count,
        Func<TEntity, TMessage> valueMapper,
        int jobId)
    {
        JobHistory job = null;
        try
        {
            logger.LogInformation("Start sending rebuild view requests.");
            while (true)
            {
                job = await db.JobHistories.FindAsync(jobId);

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

                IQueryable<TEntity> failedEventsQuery = db.Set<TEntity>();
                failedEventsQuery = isAsc ? failedEventsQuery.OrderBy(orderBy) : failedEventsQuery.OrderByDescending(orderBy);

                failedEventsQuery = failedEventsQuery.Take(count ?? BatchSize);

                var failedEvents = await failedEventsQuery.ToListAsync();

                logger.LogInformation("Retrieved {count} failures.", failedEvents.Count);

                if (failedEvents.Count == 0 || count.HasValue)
                    break;

                await Process(topic, failedEvents, valueMapper);
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
                await db.SaveChangesAsync();
            }
        }
    }

    private async Task Process(string topic, List<TEntity> failedEvents, Func<TEntity, TMessage> msgMapper)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        failedEvents.ForEach(e => producer.ProduceAsync(msgMapper(e), topic, default));
        db.Set<TEntity>().RemoveRange(failedEvents);
        await db.SaveChangesAsync();
    }

    public async Task<string> StopJob(string topic)
    {
        var jobs = await db.JobHistories.Where(j => j.Name == topic && j.FinishedAt == null).ToListAsync();

        if (jobs.Count == 0)
        {
            return $"No {topic} jobs in progress";
        }

        foreach (var job in jobs)
        {
            job.ErrorMessage = "Stopped manually.";
            job.IsSuccess = false;
            job.FinishedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync();

        return $"{topic}: stopped {jobs.Count} job(s).";
    }

    public async Task<FailedViewsResponse<TId>> GetFailedViewsOverview<TId, TKey>(Func<TEntity, TId> idSelector, Expression<Func<TEntity, TKey>> orderBy, bool isAsc)
    {
        IQueryable<TEntity> failedEventsQuery = db.Set<TEntity>();
        failedEventsQuery = isAsc ? failedEventsQuery.OrderBy(orderBy) : failedEventsQuery.OrderByDescending(orderBy);

        failedEventsQuery = failedEventsQuery.Take(10);

        var failedEvents = await failedEventsQuery.ToListAsync();

        return new FailedViewsResponse<TId>
        {
            Count = failedEvents.Count,
            TopIds = [.. failedEvents.Select(e => idSelector(e))]
        };
    }
}
