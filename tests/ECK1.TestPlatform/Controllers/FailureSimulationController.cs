using ECK1.TestPlatform.Services;
using Microsoft.AspNetCore.Mvc;

namespace ECK1.TestPlatform.Controllers;

/// <summary>
/// Orchestrates complete failure scenarios for resilience testing.
/// Each endpoint activates chaos on target proxies, performs operations, then deactivates.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class FailureSimulationController(
    ChaosOrchestratorService chaos,
    CommandsApiClient commands,
    FakeSampleDataFactory fakeSample,
    ILogger<FailureSimulationController> logger) : ControllerBase
{
    /// <summary>
    /// Creates entities while chaos is active — data never reaches targeted proxies.
    /// Reconciler (drop-event, push-noop) or FailedViewRebuilder (cache-miss, push-fail) should recover.
    /// Also covers "targeted plugin failure" scenarios by specifying which plugins to target.
    /// </summary>
    [HttpPost("missing-data")]
    public async Task<ActionResult<MissingDataResult>> MissingData(
        [FromQuery] int count = 5,
        [FromQuery(Name = "scenario_id")] string scenarioId = "proxy.drop-event",
        [FromQuery(Name = "plugins")] string[]? plugins = null,
        CancellationToken ct = default)
    {
        List<string> targets = ResolvePlugins(plugins);

        await chaos.ActivateOnPluginsAsync(targets, scenarioId, ct);
        try
        {
            var createdIds = new List<Guid>();
            int failed = 0;

            for (int i = 0; i < count; i++)
            {
                var accepted = await commands.CreateSampleAsync(fakeSample.CreateSample(true), ct);
                if (accepted is not null)
                    createdIds.Add(accepted.Id);
                else
                    failed++;
            }

            logger.LogInformation(
                "MissingData: created {Created}/{Total} entities with '{Scenario}' on [{Plugins}]",
                createdIds.Count, count, scenarioId, string.Join(", ", targets));

            return Ok(new MissingDataResult(
                Scenario: "missing-data",
                ScenarioId: scenarioId,
                TargetPlugins: targets,
                CreatedIds: createdIds,
                EntitiesCreated: createdIds.Count,
                EntitiesFailed: failed));
        }
        finally
        {
            await chaos.DeactivateOnPluginsAsync(targets, scenarioId, ct);
        }
    }

    /// <summary>
    /// Creates entities normally, waits for propagation, then applies updates with chaos active.
    /// Result: data exists in proxies but is stale (old version).
    /// </summary>
    [HttpPost("stale-data")]
    public async Task<ActionResult<StaleDataResult>> StaleData(
        [FromQuery] int count = 5,
        [FromQuery(Name = "updates_per_entity")] int updatesPerEntity = 2,
        [FromQuery(Name = "scenario_id")] string scenarioId = "proxy.drop-event",
        [FromQuery(Name = "plugins")] string[]? plugins = null,
        [FromQuery(Name = "propagation_delay_sec")] int propagationDelaySec = 5,
        CancellationToken ct = default)
    {
        List<string> targets = ResolvePlugins(plugins);

        // Phase 1: create entities (no chaos — data propagates normally)
        var createdIds = new List<Guid>();
        int createFailed = 0;
        for (int i = 0; i < count; i++)
        {
            var accepted = await commands.CreateSampleAsync(fakeSample.CreateSample(true), ct);
            if (accepted is not null)
                createdIds.Add(accepted.Id);
            else
                createFailed++;
        }

        logger.LogInformation(
            "StaleData: created {Count} entities, waiting {Delay}s for propagation",
            createdIds.Count, propagationDelaySec);

        // Phase 2: wait for data to propagate to all proxies
        await Task.Delay(TimeSpan.FromSeconds(propagationDelaySec), ct);

        // Phase 3: activate chaos, then update entities
        await chaos.ActivateOnPluginsAsync(targets, scenarioId, ct);
        int updatesApplied = 0;
        int updatesFailed = 0;
        try
        {
            foreach (Guid id in createdIds)
            {
                for (int u = 0; u < updatesPerEntity; u++)
                {
                    var accepted = await commands.ChangeSampleNameAsync(id, fakeSample.NewName(), ct);
                    if (accepted is not null)
                        updatesApplied++;
                    else
                        updatesFailed++;
                }
            }

            logger.LogInformation(
                "StaleData: applied {Updates} updates with '{Scenario}' on [{Plugins}]",
                updatesApplied, scenarioId, string.Join(", ", targets));
        }
        finally
        {
            await chaos.DeactivateOnPluginsAsync(targets, scenarioId, ct);
        }

        return Ok(new StaleDataResult(
            Scenario: "stale-data",
            ScenarioId: scenarioId,
            TargetPlugins: targets,
            CreatedIds: createdIds,
            EntitiesCreated: createdIds.Count,
            CreatesFailed: createFailed,
            UpdatesApplied: updatesApplied,
            UpdatesFailed: updatesFailed));
    }

    /// <summary>
    /// Combines missing and stale data in one batch.
    /// Creates "stale" entities first (propagate normally), then activates chaos and
    /// creates "missing" entities + updates "stale" entities — both in the same chaos window.
    /// Useful for testing reconciliation batches with mixed failure types.
    /// </summary>
    [HttpPost("mixed-batch")]
    public async Task<ActionResult<MixedBatchResult>> MixedBatch(
        [FromQuery(Name = "missing_count")] int missingCount = 3,
        [FromQuery(Name = "stale_count")] int staleCount = 3,
        [FromQuery(Name = "updates_per_stale")] int updatesPerStale = 2,
        [FromQuery(Name = "scenario_id")] string scenarioId = "proxy.drop-event",
        [FromQuery(Name = "plugins")] string[]? plugins = null,
        [FromQuery(Name = "propagation_delay_sec")] int propagationDelaySec = 5,
        CancellationToken ct = default)
    {
        List<string> targets = ResolvePlugins(plugins);

        // Phase 1: create "stale" entities (no chaos)
        var staleIds = new List<Guid>();
        for (int i = 0; i < staleCount; i++)
        {
            var accepted = await commands.CreateSampleAsync(fakeSample.CreateSample(true), ct);
            if (accepted is not null)
                staleIds.Add(accepted.Id);
        }

        logger.LogInformation(
            "MixedBatch: created {StaleCount} stale entities, waiting {Delay}s",
            staleIds.Count, propagationDelaySec);

        await Task.Delay(TimeSpan.FromSeconds(propagationDelaySec), ct);

        // Phase 2: activate chaos, create missing + update stale
        await chaos.ActivateOnPluginsAsync(targets, scenarioId, ct);
        var missingIds = new List<Guid>();
        int updatesApplied = 0;
        try
        {
            // Create missing entities (will not propagate)
            for (int i = 0; i < missingCount; i++)
            {
                var accepted = await commands.CreateSampleAsync(fakeSample.CreateSample(true), ct);
                if (accepted is not null)
                    missingIds.Add(accepted.Id);
            }

            // Update stale entities (updates will not propagate)
            foreach (Guid id in staleIds)
            {
                for (int u = 0; u < updatesPerStale; u++)
                {
                    var accepted = await commands.ChangeSampleNameAsync(id, fakeSample.NewName(), ct);
                    if (accepted is not null)
                        updatesApplied++;
                }
            }

            logger.LogInformation(
                "MixedBatch: {Missing} missing + {Stale} stale ({Updates} updates) with '{Scenario}' on [{Plugins}]",
                missingIds.Count, staleIds.Count, updatesApplied, scenarioId, string.Join(", ", targets));
        }
        finally
        {
            await chaos.DeactivateOnPluginsAsync(targets, scenarioId, ct);
        }

        return Ok(new MixedBatchResult(
            Scenario: "mixed-batch",
            ScenarioId: scenarioId,
            TargetPlugins: targets,
            MissingEntityIds: missingIds,
            StaleEntityIds: staleIds,
            MissingCreated: missingIds.Count,
            StaleCreated: staleIds.Count,
            UpdatesApplied: updatesApplied));
    }

    /// <summary>
    /// Creates version gaps by toggling chaos on/off during sequential updates.
    /// Example: versions 1,2,_,4,5,_,7 where _ is a dropped update.
    /// Primarily targets Clickhouse where version continuity matters.
    /// </summary>
    [HttpPost("version-gap")]
    public async Task<ActionResult<VersionGapResult>> VersionGap(
        [FromQuery(Name = "total_updates")] int totalUpdates = 10,
        [FromQuery(Name = "gap_every")] int gapEvery = 3,
        [FromQuery(Name = "gap_duration")] int gapDuration = 1,
        [FromQuery(Name = "scenario_id")] string scenarioId = "proxy.drop-event",
        [FromQuery] string plugin = "clickhouse",
        [FromQuery(Name = "propagation_delay_sec")] int propagationDelaySec = 5,
        CancellationToken ct = default)
    {
        var targets = new List<string> { plugin };

        // Phase 1: create entity (no chaos)
        var accepted = await commands.CreateSampleAsync(fakeSample.CreateSample(true), ct);
        if (accepted is null)
            return StatusCode(502, "Failed to create sample entity via CommandsAPI");

        Guid entityId = accepted.Id;

        logger.LogInformation(
            "VersionGap: created entity {Id}, waiting {Delay}s for propagation",
            entityId, propagationDelaySec);

        await Task.Delay(TimeSpan.FromSeconds(propagationDelaySec), ct);

        // Phase 2: apply updates with chaos toggling
        var droppedVersions = new List<int>();
        int appliedCount = 0;
        int droppedCount = 0;
        bool chaosActive = false;

        try
        {
            for (int i = 1; i <= totalUpdates; i++)
            {
                // Determine if this update should be dropped
                bool shouldDrop = ShouldDropAtIndex(i, gapEvery, gapDuration);

                if (shouldDrop && !chaosActive)
                {
                    await chaos.ActivateOnPluginsAsync(targets, scenarioId, ct);
                    chaosActive = true;
                }
                else if (!shouldDrop && chaosActive)
                {
                    await chaos.DeactivateOnPluginsAsync(targets, scenarioId, ct);
                    chaosActive = false;
                }

                var result = await commands.ChangeSampleNameAsync(entityId, fakeSample.NewName(), ct);
                if (result is not null)
                {
                    if (shouldDrop)
                    {
                        droppedVersions.Add(i + 1); // +1 because version 1 is the create
                        droppedCount++;
                    }
                    else
                    {
                        appliedCount++;
                    }
                }
            }
        }
        finally
        {
            if (chaosActive)
                await chaos.DeactivateOnPluginsAsync(targets, scenarioId, ct);
        }

        logger.LogInformation(
            "VersionGap: entity {Id}, {Applied} applied / {Dropped} dropped, missing versions: [{Versions}]",
            entityId, appliedCount, droppedCount, string.Join(", ", droppedVersions));

        return Ok(new VersionGapResult(
            Scenario: "version-gap",
            ScenarioId: scenarioId,
            TargetPlugin: plugin,
            EntityId: entityId,
            TotalUpdates: totalUpdates,
            AppliedUpdates: appliedCount,
            DroppedUpdates: droppedCount,
            ExpectedMissingVersions: droppedVersions));
    }

    private List<string> ResolvePlugins(string[]? plugins)
    {
        if (plugins is null || plugins.Length == 0)
            return [.. chaos.AllPluginNames];
        return [.. plugins];
    }

    private static bool ShouldDropAtIndex(int index, int gapEvery, int gapDuration)
    {
        if (gapEvery <= 0) return false;
        int position = (index - 1) % (gapEvery + gapDuration);
        return position >= gapEvery;
    }
}

public sealed record MissingDataResult(
    string Scenario,
    string ScenarioId,
    List<string> TargetPlugins,
    List<Guid> CreatedIds,
    int EntitiesCreated,
    int EntitiesFailed);

public sealed record StaleDataResult(
    string Scenario,
    string ScenarioId,
    List<string> TargetPlugins,
    List<Guid> CreatedIds,
    int EntitiesCreated,
    int CreatesFailed,
    int UpdatesApplied,
    int UpdatesFailed);

public sealed record MixedBatchResult(
    string Scenario,
    string ScenarioId,
    List<string> TargetPlugins,
    List<Guid> MissingEntityIds,
    List<Guid> StaleEntityIds,
    int MissingCreated,
    int StaleCreated,
    int UpdatesApplied);

public sealed record VersionGapResult(
    string Scenario,
    string ScenarioId,
    string TargetPlugin,
    Guid EntityId,
    int TotalUpdates,
    int AppliedUpdates,
    int DroppedUpdates,
    List<int> ExpectedMissingVersions);
