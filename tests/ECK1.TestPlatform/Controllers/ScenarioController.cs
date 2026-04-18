using ECK1.TestPlatform.Data;
using ECK1.TestPlatform.Hubs;
using ECK1.TestPlatform.Scenarios;
using ECK1.TestPlatform.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace ECK1.TestPlatform.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ScenarioController(
    ScenarioRegistry registry,
    IHubContext<ScenarioHub> hubContext,
    RunStore runStore,
    ChaosManager chaosManager,
    BearerTokenStore tokenStore,
    ILogger<ScenarioController> logger) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<ScenarioDefinition>> List()
    {
        return Ok(registry.GetAll());
    }

    [HttpGet("{scenarioId}")]
    public ActionResult<ScenarioDefinition> Get(string scenarioId)
    {
        var scenario = registry.Get(scenarioId);
        if (scenario is null)
            return NotFound(new { error = $"Scenario '{scenarioId}' not found" });

        return Ok(scenario.Definition);
    }

    [HttpPost("run")]
    public ActionResult<ScenarioRunResponse> Run([FromBody] ScenarioRunRequest request)
    {
        var scenario = registry.Get(request.ScenarioId);
        if (scenario is null)
            return NotFound(new { error = $"Scenario '{request.ScenarioId}' not found" });

        // Enforce single run at a time
        if (!runStore.TryAcquireRunLock())
        {
            return Conflict(new
            {
                error = "Another scenario is already running",
                activeRunId = runStore.ActiveRunId
            });
        }

        string runId = Guid.NewGuid().ToString("N");
        var cancellationSource = new CancellationTokenSource();

        // Resolve settings: merge user-provided with defaults
        var resolvedSettings = new Dictionary<string, object>();
        foreach (var setting in scenario.Definition.Settings)
        {
            if (request.Settings is not null && request.Settings.TryGetValue(setting.Key, out var userValue))
                resolvedSettings[setting.Key] = userValue;
            else
                resolvedSettings[setting.Key] = setting.DefaultValue;
        }

        var context = new ScenarioRunContext(
            runId,
            scenario.Definition.Id,
            scenario.Definition.Name,
            scenario.Definition.StepNames,
            resolvedSettings,
            hubContext,
            runStore,
            logger);

        // Track in RunStore so hub can send initial state on subscribe
        runStore.Track(context, cancellationSource);

        // Forward the caller's auth token for service-to-service calls.
        tokenStore.SetAuthorizationHeader(HttpContext.Request.Headers.Authorization.FirstOrDefault());

        // Fire-and-forget: run scenario in background
        _ = Task.Run(async () =>
        {
            try
            {
                // Pre-cleanup: reset all chaos before starting
                await ResetAllChaosAsync();

                await scenario.RunAsync(context, cancellationSource.Token);
            }
            catch (OperationCanceledException) when (cancellationSource.IsCancellationRequested)
            {
                logger.LogInformation("Scenario run {RunId} cancellation observed by controller", runId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scenario run {RunId} failed unexpectedly", runId);
                await context.FinishAsync(false, ex.Message);
            }
            finally
            {
                tokenStore.SetToken(null);
                runStore.ReleaseRunLock();
            }
        });

        return Accepted(new ScenarioRunResponse(
            RunId: runId,
            ScenarioId: scenario.Definition.Id,
            ScenarioName: scenario.Definition.Name,
            ResolvedSettings: resolvedSettings,
            StepNames: scenario.Definition.StepNames));
    }

    [HttpGet("runs")]
    public async Task<ActionResult<List<RunSummary>>> GetRecentRuns([FromQuery] int count = 50)
    {
        var runs = await runStore.GetRecentRunsAsync(Math.Clamp(count, 1, 200));
        return Ok(runs);
    }

    [HttpGet("runs/{runId}")]
    public async Task<ActionResult<ScenarioProgress>> GetRun(string runId)
    {
        var progress = await runStore.GetProgressAsync(runId);
        if (progress is null)
            return NotFound(new { error = $"Run '{runId}' not found" });

        return Ok(progress);
    }

    [HttpPost("runs/{runId}/cancel")]
    public async Task<ActionResult> CancelRun(string runId)
    {
        var result = runStore.RequestCancellation(runId);
        if (result == RunCancellationRequestResult.Requested)
            return Accepted(new { runId, message = "Cancellation requested" });

        if (result == RunCancellationRequestResult.AlreadyRequested)
            return Accepted(new { runId, message = "Cancellation was already requested" });

        var progress = await runStore.GetProgressAsync(runId);
        if (progress is null)
            return NotFound(new { error = $"Run '{runId}' not found" });

        return Conflict(new
        {
            error = progress.IsCompleted
                ? $"Run '{runId}' is already completed"
                : $"Run '{runId}' cannot be cancelled"
        });
    }

    [HttpGet("status")]
    public ActionResult GetStatus()
    {
        return Ok(new
        {
            hasActiveRun = runStore.HasActiveRun,
            activeRunId = runStore.ActiveRunId
        });
    }

    private async Task ResetAllChaosAsync()
    {
        try
        {
            logger.LogInformation("Pre-cleanup: resetting all chaos on all services");
            var allTargets = chaosManager.AllPluginNames.Append("reconciler");
            await chaosManager.DeactivateAllOnAsync(allTargets, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Pre-cleanup: failed to reset some chaos (continuing anyway)");
        }
    }
}
