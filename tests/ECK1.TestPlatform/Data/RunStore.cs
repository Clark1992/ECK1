using System.Collections.Concurrent;
using System.Text.Json;
using ECK1.TestPlatform.Scenarios;
using Microsoft.EntityFrameworkCore;

namespace ECK1.TestPlatform.Data;

public sealed class RunStore(IServiceScopeFactory scopeFactory, ILogger<RunStore> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ConcurrentDictionary<string, ScenarioRunContext> _activeRuns = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _runCancellationSources = new();
    private readonly SemaphoreSlim _runLock = new(1, 1);

    public bool HasActiveRun => !_activeRuns.IsEmpty;

    public string? ActiveRunId => _activeRuns.Keys.FirstOrDefault();

    /// <summary>
    /// Tries to acquire the single-run lock. Returns true if acquired.
    /// Caller must call ReleaseRunLock when done.
    /// </summary>
    public bool TryAcquireRunLock()
    {
        return _runLock.Wait(0);
    }

    public void ReleaseRunLock()
    {
        _runLock.Release();
    }

    public void Track(ScenarioRunContext context, CancellationTokenSource cancellationSource)
    {
        _activeRuns[context.RunId] = context;
        _runCancellationSources[context.RunId] = cancellationSource;
    }

    public ScenarioProgress? GetActiveProgress(string runId)
    {
        return _activeRuns.TryGetValue(runId, out var ctx) ? ctx.BuildProgress() : null;
    }

    public void Complete(string runId)
    {
        _activeRuns.TryRemove(runId, out _);

        if (_runCancellationSources.TryRemove(runId, out var cancellationSource))
            cancellationSource.Dispose();
    }

    public RunCancellationRequestResult RequestCancellation(string runId)
    {
        if (!_activeRuns.ContainsKey(runId) || !_runCancellationSources.TryGetValue(runId, out var cancellationSource))
            return RunCancellationRequestResult.NotFound;

        if (cancellationSource.IsCancellationRequested)
            return RunCancellationRequestResult.AlreadyRequested;

        try
        {
            cancellationSource.Cancel();
            return RunCancellationRequestResult.Requested;
        }
        catch (ObjectDisposedException)
        {
            return RunCancellationRequestResult.NotFound;
        }
    }

    public async Task PersistAsync(ScenarioRunContext context)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RunHistoryDb>();

            var progress = context.BuildProgress();
            var record = await db.Runs.FindAsync(context.RunId);

            if (record is null)
            {
                record = new ScenarioRunRecord
                {
                    RunId = context.RunId,
                    ScenarioId = context.ScenarioId,
                    ScenarioName = context.ScenarioName,
                    StartedAt = context.StartedAt,
                    SettingsJson = JsonSerializer.Serialize(context.Settings, JsonOptions),
                };
                db.Runs.Add(record);
            }

            record.IsCompleted = context.IsCompleted;
            record.IsSuccess = context.IsSuccess;
            record.Error = context.Error;
            record.CompletedAt = context.CompletedAt;
            record.ProgressJson = JsonSerializer.Serialize(progress, JsonOptions);

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist run {RunId} to history DB", context.RunId);
        }
    }

    public async Task<ScenarioProgress?> GetProgressAsync(string runId)
    {
        // Active run — return live state
        if (_activeRuns.TryGetValue(runId, out var ctx))
            return ctx.BuildProgress();

        // Historical run — load from DB
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RunHistoryDb>();
            var record = await db.Runs.AsNoTracking().FirstOrDefaultAsync(r => r.RunId == runId);
            if (record is null) return null;

            return JsonSerializer.Deserialize<ScenarioProgress>(record.ProgressJson, JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load run {RunId} from history DB", runId);
            return null;
        }
    }

    public async Task<List<RunSummary>> GetRecentRunsAsync(int count = 50)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RunHistoryDb>();

            return await db.Runs
                .AsNoTracking()
                .OrderByDescending(r => r.StartedAt)
                .Take(count)
                .Select(r => new RunSummary(
                    r.RunId,
                    r.ScenarioId,
                    r.ScenarioName,
                    r.IsCompleted,
                    r.IsSuccess,
                    r.Error,
                    r.StartedAt,
                    r.CompletedAt))
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load recent runs from history DB");
            return [];
        }
    }
}

public sealed record RunSummary(
    string RunId,
    string ScenarioId,
    string ScenarioName,
    bool IsCompleted,
    bool IsSuccess,
    string? Error,
    DateTime StartedAt,
    DateTime? CompletedAt);

public enum RunCancellationRequestResult
{
    NotFound,
    Requested,
    AlreadyRequested
}
