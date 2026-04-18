namespace ECK1.TestPlatform.Scenarios;

public enum StepStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Cancelled
}

public sealed record StepProgress(
    int StepIndex,
    string StepName,
    StepStatus Status,
    string? Message = null,
    Dictionary<string, object>? Data = null,
    DateTime? StartedAt = null,
    DateTime? CompletedAt = null);

public sealed record ScenarioProgress(
    string RunId,
    string ScenarioId,
    string ScenarioName,
    bool IsRunning,
    bool IsCompleted,
    bool IsSuccess,
    bool IsCancelled,
    string? Error,
    List<StepProgress> Steps,
    Dictionary<string, object>? Settings,
    DateTime StartedAt,
    DateTime? CompletedAt);

public sealed record ScenarioDefinition(
    string Id,
    string Name,
    string Description,
    List<ScenarioSettingDefinition> Settings,
    List<string> StepNames);

public sealed record ScenarioSettingDefinition(
    string Key,
    string Label,
    string Description,
    string Type,
    object DefaultValue,
    object? Min = null,
    object? Max = null,
    List<string>? Options = null);

public sealed record ScenarioRunRequest(
    string ScenarioId,
    Dictionary<string, object>? Settings);

public sealed record ScenarioRunResponse(
    string RunId,
    string ScenarioId,
    string ScenarioName,
    Dictionary<string, object> ResolvedSettings,
    List<string> StepNames);
