using System.Text.Json;
using ECK1.TestPlatform.Data;
using ECK1.TestPlatform.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace ECK1.TestPlatform.Scenarios;

public sealed class ScenarioRunContext
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHubContext<ScenarioHub> _hub;
    private readonly RunStore _runStore;
    private readonly ILogger _logger;
    private readonly List<StepProgress> _steps;

    public string RunId { get; }
    public string ScenarioId { get; }
    public string ScenarioName { get; }
    public Dictionary<string, object> Settings { get; }
    public DateTime StartedAt { get; }
    public DateTime? CompletedAt { get; private set; }
    public bool IsCompleted { get; private set; }
    public bool IsSuccess { get; private set; }
    public bool IsCancelled { get; private set; }
    public string? Error { get; private set; }

    public ScenarioRunContext(
        string runId,
        string scenarioId,
        string scenarioName,
        List<string> stepNames,
        Dictionary<string, object> settings,
        IHubContext<ScenarioHub> hub,
        RunStore runStore,
        ILogger logger)
    {
        RunId = runId;
        ScenarioId = scenarioId;
        ScenarioName = scenarioName;
        Settings = settings;
        _hub = hub;
        _runStore = runStore;
        _logger = logger;
        StartedAt = DateTime.UtcNow;

        _steps = stepNames.Select((name, i) => new StepProgress(i, name, StepStatus.Pending)).ToList();
    }

    public T GetSetting<T>(string key, T defaultValue)
    {
        if (!Settings.TryGetValue(key, out var raw))
            return defaultValue;

        if (raw is T typed)
            return typed;

        if (raw is JsonElement jsonElement)
        {
            var json = jsonElement.GetRawText();
            return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? defaultValue;
        }

        try
        {
            return (T)Convert.ChangeType(raw, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    public async Task BeginStepAsync(int stepIndex, string? message = null)
    {
        if (IsCompleted) return;

        _steps[stepIndex] = _steps[stepIndex] with { Status = StepStatus.InProgress, Message = message, StartedAt = DateTime.UtcNow };
        _logger.LogInformation("[Run {RunId}] Step {Step} started: {Name} {Msg}",
            RunId, stepIndex, _steps[stepIndex].StepName, message ?? "");
        await BroadcastProgressAsync();
    }

    public async Task UpdateStepAsync(int stepIndex, string? message = null, Dictionary<string, object>? data = null)
    {
        if (IsCompleted) return;

        _steps[stepIndex] = _steps[stepIndex] with
        {
            Message = message ?? _steps[stepIndex].Message,
            Data = data ?? _steps[stepIndex].Data
        };
        await BroadcastProgressAsync();
    }

    public async Task CompleteStepAsync(int stepIndex, string? message = null, Dictionary<string, object>? data = null)
    {
        if (IsCompleted) return;

        _steps[stepIndex] = _steps[stepIndex] with
        {
            Status = StepStatus.Completed,
            Message = message ?? _steps[stepIndex].Message,
            Data = data ?? _steps[stepIndex].Data,
            CompletedAt = DateTime.UtcNow
        };
        _logger.LogInformation("[Run {RunId}] Step {Step} completed: {Name}",
            RunId, stepIndex, _steps[stepIndex].StepName);
        await BroadcastProgressAsync();
    }

    public async Task FailStepAsync(int stepIndex, string error, Dictionary<string, object>? data = null)
    {
        if (IsCompleted) return;

        _steps[stepIndex] = _steps[stepIndex] with
        {
            Status = StepStatus.Failed,
            Message = error,
            Data = data ?? _steps[stepIndex].Data,
            CompletedAt = DateTime.UtcNow
        };
        _logger.LogError("[Run {RunId}] Step {Step} failed: {Name} — {Error}",
            RunId, stepIndex, _steps[stepIndex].StepName, error);
        await BroadcastProgressAsync();
    }

    public async Task FinishAsync(bool success, string? error = null)
    {
        if (IsCompleted) return;

        IsCompleted = true;
        IsSuccess = success;
        IsCancelled = false;
        Error = error;
        CompletedAt = DateTime.UtcNow;
        await BroadcastProgressAsync();
        // Final persist + remove from active tracking
        await _runStore.PersistAsync(this);
        _runStore.Complete(RunId);
    }

    public async Task CancelAsync(string? error = null)
    {
        if (IsCompleted) return;

        var completedAt = DateTime.UtcNow;
        var finalError = string.IsNullOrWhiteSpace(error) ? "Scenario was cancelled" : error;

        for (int i = 0; i < _steps.Count; i++)
        {
            var step = _steps[i];
            if (step.Status is StepStatus.Completed or StepStatus.Failed or StepStatus.Cancelled)
                continue;

            _steps[i] = step with
            {
                Status = StepStatus.Cancelled,
                Message = step.Status == StepStatus.InProgress
                    ? finalError
                    : "Skipped because the run was cancelled",
                CompletedAt = completedAt
            };
        }

        _logger.LogInformation("[Run {RunId}] Scenario cancelled", RunId);

        IsCompleted = true;
        IsSuccess = false;
        IsCancelled = true;
        Error = finalError;
        CompletedAt = completedAt;
        await BroadcastProgressAsync();
        await _runStore.PersistAsync(this);
        _runStore.Complete(RunId);
    }

    public ScenarioProgress BuildProgress() => new(
        RunId: RunId,
        ScenarioId: ScenarioId,
        ScenarioName: ScenarioName,
        IsRunning: !IsCompleted,
        IsCompleted: IsCompleted,
        IsSuccess: IsSuccess,
        IsCancelled: IsCancelled,
        Error: Error,
        Steps: [.. _steps],
        Settings: Settings,
        StartedAt: StartedAt,
        CompletedAt: CompletedAt);

    private async Task BroadcastProgressAsync()
    {
        var progress = BuildProgress();
        await _hub.Clients.Group($"run:{RunId}").SendAsync("ScenarioProgress", progress);
        await _runStore.PersistAsync(this);
    }
}
