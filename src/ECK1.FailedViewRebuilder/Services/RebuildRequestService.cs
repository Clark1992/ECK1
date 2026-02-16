using ECK1.CommonUtils.JobQueue;
using ECK1.FailedViewRebuilder.Data;
using ECK1.FailedViewRebuilder.Data.Models;
using ECK1.FailedViewRebuilder.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Options;

namespace ECK1.FailedViewRebuilder.Services;

public class FailedViewsResponse
{
    public int Count { get; set; }
    public List<(Guid, string)> TopIdsWithTypes { get; set; }
}

public interface IRebuildRequestService
{
    Task<string> StartJob(
        string entityType,
        int? count);

    Task<int> StopJob(string entityType);

    Task<FailedViewsResponse> GetFailedViewsOverview(string entityType, int? count);

    Task<FailedViewsResponse> GetFailedViewsOverview(int? count);

    Task<int> GetStatus(string entityType);
}

public class RebuildRequestService(
    FailuresDbContext db,
    ILogger<RebuildRequestService> logger,
    IBackgroundTaskQueue taskQueue,
    IOptionsSnapshot<FailureHandlingConfig> config) : IRebuildRequestService
{
    public async Task<string> StartJob(
        string entityType,
        int? count)
    {
        if(!config.Value.TryGetValue(entityType, out var entry))
        {
            var msg = "Cannot find failure handling config for {0}";
            logger.LogWarning(msg, entityType);
            return string.Format(msg, entityType);
        }

        var jobName = entry.RebuildRequestTopic;

        var job = await db.JobHistories.FirstOrDefaultAsync(j => j.Name == jobName && j.FinishedAt == null);

        if (job is not null)
        {
            logger.LogInformation("Previous job ({topic}) is still in progress", jobName);

            return $"[{entityType}] Previous job ({jobName}) started at {job.StartedAt} is still in progress.";
        }

        job = new JobHistory
        {
            Name = jobName,
            StartedAt = DateTimeOffset.UtcNow,
        };

        db.JobHistories.Add(job);

        await db.SaveChangesAsync();

        var qParams = new QueryParams<DateTimeOffset>
                    {
                        Filter = e => e.EntityType == entityType,
                        OrderBy = e => e.FailureOccurredAt,
                        IsAsc = true,
                        Count = count
                    };

        taskQueue.QueueBackgroundWorkItem(async (sp, ct) =>
        {
            var jobRunner = sp.GetRequiredService<IJobRunner>();
            await jobRunner.RunJob(jobName, qParams, job.Id, ct);
        });

        return $"[{entityType}] Started {job.Name} at {job.StartedAt}";
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

    public Task<FailedViewsResponse> GetFailedViewsOverview(
        string entityType,
        int? count) => 
        GetFailedViewsOverview(new QueryParams<DateTimeOffset>
        {
            Filter = e => e.EntityType == entityType,
            OrderBy = e => e.FailureOccurredAt,
            IsAsc = true,
            Count = count
        });

    public Task<FailedViewsResponse> GetFailedViewsOverview(int? count) => 
        GetFailedViewsOverview(new QueryParams<DateTimeOffset>
        {
            OrderBy = e => e.FailureOccurredAt,
            IsAsc = true,
            Count = count
        });

    private async Task<FailedViewsResponse> GetFailedViewsOverview<TKey>(QueryParams<TKey> qParams)
    {
        IQueryable<EventFailure> failedEventsQuery = db.Set<EventFailure>();

        if (qParams.Filter is not null)
        {
            failedEventsQuery = failedEventsQuery.Where(qParams.Filter);
        }

        failedEventsQuery = qParams.IsAsc ? failedEventsQuery.OrderBy(qParams.OrderBy) : failedEventsQuery.OrderByDescending(qParams.OrderBy);

        failedEventsQuery = failedEventsQuery.Take(qParams.Count ?? 10);

        var failedEvents = await failedEventsQuery.ToListAsync();

        return new FailedViewsResponse
        {
            Count = failedEvents.Count,
            TopIdsWithTypes = [.. failedEvents.Select(e => (e.EntityId, e.EntityType))]
        };
    }
}