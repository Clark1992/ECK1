using ECK1.CommonUtils.JobQueue;
using ECK1.FailedViewRebuilder.Data;
using ECK1.FailedViewRebuilder.Data.Models;
using ECK1.FailedViewRebuilder.Models;
using Microsoft.EntityFrameworkCore;

namespace ECK1.FailedViewRebuilder.Services;

public class FailedViewsResponse<TId>
{
    public int Count { get; set; }
    public List<TId> TopIds { get; set; }
}

public interface IRebuildRequestService<TEntity, TMessage> where TEntity : class
{
    Task<string> StartJob<TKey>(
        string jobName,
        QueryParams<TEntity, TKey> qParams,
        Func<TEntity, TMessage> valueMapper);

    Task<int> StopJob(string jobName);

    Task<FailedViewsResponse<TId>> GetFailedViewsOverview<TId, TKey>(
        Func<TEntity, TId> idSelector,
        QueryParams<TEntity, TKey> qParams);

    Task<int> GetStatus(string jobName);
}

public class RebuildRequestService<TEntity, TMessage>(
    FailuresDbContext db,
    ILogger<RebuildRequestService<TEntity, TMessage>> logger,
    IBackgroundTaskQueue taskQueue) : IRebuildRequestService<TEntity, TMessage> where TEntity : class
{
    public async Task<string> StartJob<TKey>(
        string jobName,
        QueryParams<TEntity, TKey> qParams,
        Func<TEntity, TMessage> valueMapper)
    {
        var job = await db.JobHistories.FirstOrDefaultAsync(j => j.Name == jobName && j.FinishedAt == null);

        if (job is not null)
        {
            logger.LogInformation("Previous job ({topic}) is still in progress", jobName);

            return $"Previous job ({jobName}) started at {job.StartedAt} is still in progress.";
        }

        job = new JobHistory
        {
            Name = jobName,
            StartedAt = DateTimeOffset.UtcNow,
        };

        db.JobHistories.Add(job);

        await db.SaveChangesAsync();

        taskQueue.QueueBackgroundWorkItem(async (sp, ct) =>
        {
            var jobRunner = sp.GetRequiredService<IJobRunner<TEntity, TMessage>>();
            await jobRunner.RunJob(jobName, qParams, valueMapper, job.Id, ct);
        });

        return $"Started {job.Name} at {job.StartedAt}";
    }

    public Task<int> GetStatus(string jobName) => 
        db.JobHistories.CountAsync(j => j.Name == jobName && j.FinishedAt == null);

    public async Task<int> StopJob(string jobName)
    {
        var jobs = await db.JobHistories.Where(j => j.Name == jobName && j.FinishedAt == null).ToListAsync();

        if (jobs.Count == 0)
        {
            return 0;
        }

        foreach (var job in jobs)
        {
            job.ErrorMessage = "Stopped manually.";
            job.IsSuccess = false;
            job.FinishedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync();

        return jobs.Count;
    }

    public async Task<FailedViewsResponse<TId>> GetFailedViewsOverview<TId, TKey>(
        Func<TEntity, TId> idSelector,
        QueryParams<TEntity, TKey> qParams)
    {
        IQueryable<TEntity> failedEventsQuery = db.Set<TEntity>();
        failedEventsQuery = qParams.IsAsc ? failedEventsQuery.OrderBy(qParams.OrderBy) : failedEventsQuery.OrderByDescending(qParams.OrderBy);

        failedEventsQuery = failedEventsQuery.Take(qParams.Count ?? 10);

        var failedEvents = await failedEventsQuery.ToListAsync();

        return new FailedViewsResponse<TId>
        {
            Count = failedEvents.Count,
            TopIds = [.. failedEvents.Select(e => idSelector(e))]
        };
    }
}
