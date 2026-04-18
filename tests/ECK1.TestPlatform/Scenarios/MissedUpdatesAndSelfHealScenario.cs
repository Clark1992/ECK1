using ECK1.CommonUtils.Chaos;
using ECK1.TestPlatform.Services;

namespace ECK1.TestPlatform.Scenarios;

public sealed class MissedUpdatesAndSelfHealScenario(
    ChaosManager chaosManager,
    CommandsApiClient commandsClient,
    StorageVersionChecker versionChecker,
    FakeSampleDataFactory fakeSample,
    ILogger<MissedUpdatesAndSelfHealScenario> logger) : IScenario
{
    private static readonly List<string> Steps =
    [
        "Pause reconciler",
        "Create baseline entities",
        "Wait for propagation & verify",
        "Activate proxy chaos",
        "Create new & update existing entities",
        "Verify stale data in storages",
        "Deactivate proxy chaos (CH extra update)",
        "Resume reconciler",
        "Wait for self-healing",
        "Report results"
    ];

    private static string GetTargetDisplayName(string target) => target.ToLowerInvariant() switch
    {
        "mongo" => "Mongo",
        "elasticsearch" => "ES",
        "clickhouse" => "CH",
        _ => target
    };

    public ScenarioDefinition Definition { get; } = new(
        Id: "missed-updates-self-heal",
        Name: "Missed Updates in Proxy & Self-Heal",
        Description: "Creates entities, activates proxy chaos (drop/noop) to cause missed updates, " +
                     "then verifies that the reconciler detects inconsistencies and triggers rebuilds to heal the data.",
        Settings:
        [
            new("baselineCount", "Baseline Entities", "Number of entities created before chaos (will receive updates later)", "int", 3, Min: 1, Max: 20),
            new("newCount", "New Entities During Chaos", "Number of entities created while chaos is active (completely missing)", "int", 2, Min: 1, Max: 20),
            new("updatesPerEntity", "Updates Per Baseline Entity", "Number of updates applied to each baseline entity during chaos", "int", 2, Min: 1, Max: 10),
            new("proxyScenario", "Proxy Chaos Scenario", "Which proxy chaos scenario to activate", "select", ChaosScenarios.Proxy.DropEvent,
                Options: [ChaosScenarios.Proxy.DropEvent, ChaosScenarios.Proxy.PushNoop]),
            new("targetPlugins", "Target Plugins", "Which proxy plugins to target", "multiselect", "elasticsearch,mongo,clickhouse",
                Options: ["elasticsearch", "mongo", "clickhouse"]),
            new("propagationDelaySec", "Propagation Delay (sec)", "Seconds to wait for data propagation before verification", "int", 8, Min: 3, Max: 30),
            new("selfHealTimeoutSec", "Self-Heal Timeout (sec)", "Max seconds to wait for reconciler to heal data", "int", 180, Min: 30, Max: 600),
            new("pollIntervalSec", "Poll Interval (sec)", "Seconds between polling queries API during self-heal wait", "int", 5, Min: 2, Max: 30),
        ],
        StepNames: Steps);

    public async Task RunAsync(ScenarioRunContext ctx, CancellationToken ct)
    {
        int baselineCount = ctx.GetSetting("baselineCount", 3);
        int newCount = ctx.GetSetting("newCount", 2);
        int updatesPerEntity = ctx.GetSetting("updatesPerEntity", 2);
        string proxyScenario = ctx.GetSetting("proxyScenario", ChaosScenarios.Proxy.DropEvent);
        var targetPluginsRaw = ctx.GetSetting("targetPlugins", "elasticsearch,mongo,clickhouse");
        List<string> targetPlugins = [.. targetPluginsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
        int propagationDelaySec = ctx.GetSetting("propagationDelaySec", 8);
        int selfHealTimeoutSec = ctx.GetSetting("selfHealTimeoutSec", 180);
        int pollIntervalSec = ctx.GetSetting("pollIntervalSec", 5);

        var verifiable = targetPlugins.Where(StorageVersionChecker.IsVerifiable).ToList();
        var unverifiable = targetPlugins.Except(verifiable, StringComparer.OrdinalIgnoreCase).ToList();
        bool clickhouseTargeted = targetPlugins.Contains("clickhouse", StringComparer.OrdinalIgnoreCase);

        var baselineIds = new List<Guid>();
        var newIds = new List<Guid>();
        var entityVersions = new Dictionary<Guid, int>();
        var stalePairs = new HashSet<(Guid EntityId, string Target)>();
        var healedPairs = new HashSet<(Guid EntityId, string Target)>();
        bool wasCancelled = false;

        try
        {
            if (unverifiable.Count > 0)
                logger.LogWarning("[Run {RunId}] No query endpoint to verify staleness/healing for targets: {Targets}",
                    ctx.RunId, string.Join(", ", unverifiable));

            // Step 0: Pause reconciler
            var reconciler = chaosManager.GetClient("reconciler");
            await ctx.BeginStepAsync(0, "Pausing reconciler checks and dispatching...");
            await reconciler.ActivateAsync(ChaosScenarios.Reconciler.PauseChecks, ct);
            await reconciler.ActivateAsync(ChaosScenarios.Reconciler.PauseDispatching, ct);
            await ctx.CompleteStepAsync(0, "Reconciler paused");

            // Step 1: Create baseline entities
            await ctx.BeginStepAsync(1, $"Creating {baselineCount} baseline entities...");
            int baselineFailed = 0;
            for (int i = 0; i < baselineCount; i++)
            {
                var accepted = await commandsClient.CreateSampleAsync(fakeSample.CreateSample(true), ct);
                if (accepted is not null)
                {
                    baselineIds.Add(accepted.Id);
                    entityVersions[accepted.Id] = Math.Max(accepted.Version, 1);
                }
                else
                {
                    baselineFailed++;
                }
            }
            await ctx.CompleteStepAsync(1, $"Created {baselineIds.Count} baseline entities ({baselineFailed} failed)", new Dictionary<string, object>
            {
                ["baselineIds"] = baselineIds,
                ["created"] = baselineIds.Count,
                ["failed"] = baselineFailed
            });

            if (baselineIds.Count == 0)
            {
                await ctx.FailStepAsync(1, "No baseline entities were created");
                await ctx.FinishAsync(false, "Cannot proceed without baseline entities");
                return;
            }

            // Step 2: Wait for propagation and verify
            await ctx.BeginStepAsync(2, $"Waiting {propagationDelaySec}s for data propagation...");
            await Task.Delay(TimeSpan.FromSeconds(propagationDelaySec), ct);

            var verifiedCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (string target in verifiable)
                verifiedCounts[target] = 0;

            foreach (Guid id in baselineIds)
            {
                int expectedVer = entityVersions[id];
                foreach (string target in verifiable)
                {
                    if (await versionChecker.GetEntityVersionAsync(target, id, ct) == expectedVer)
                        verifiedCounts[target]++;
                }
            }

            string verificationSummary = verifiedCounts.Count == 0
                ? "no verifiable targets selected"
                : string.Join(", ",
                    verifiedCounts.Select(kvp => $"{kvp.Value}/{baselineIds.Count} in {GetTargetDisplayName(kvp.Key)}"));

            await ctx.CompleteStepAsync(2, $"Verified: {verificationSummary}", new Dictionary<string, object>
            {
                ["perTarget"] = verifiedCounts.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value),
                ["total"] = baselineIds.Count
            });

            // Step 3: Activate proxy chaos
            await ctx.BeginStepAsync(3, $"Activating '{proxyScenario}' on [{string.Join(", ", targetPlugins)}]...");
            await chaosManager.ActivateOnAsync(targetPlugins, proxyScenario, ct);
            await ctx.CompleteStepAsync(3, $"Proxy chaos '{proxyScenario}' activated on [{string.Join(", ", targetPlugins)}]");

            // Step 4: Create new entities + update existing
            await ctx.BeginStepAsync(4, $"Creating {newCount} new entities and applying {updatesPerEntity} updates to {baselineIds.Count} baseline entities...");
            int newFailed = 0;
            int updatesApplied = 0;
            int updatesFailed = 0;

            for (int i = 0; i < newCount; i++)
            {
                var accepted = await commandsClient.CreateSampleAsync(fakeSample.CreateSample(true), ct);
                if (accepted is not null)
                {
                    newIds.Add(accepted.Id);
                    entityVersions[accepted.Id] = Math.Max(accepted.Version, 1);
                }
                else
                    newFailed++;
            }

            foreach (Guid id in baselineIds)
            {
                for (int u = 0; u < updatesPerEntity; u++)
                {
                    string newName = fakeSample.NewName();
                    var accepted = await commandsClient.ChangeSampleNameAsync(id, newName, ct, entityVersions[id]);
                    if (accepted is not null)
                    {
                        updatesApplied++;
                        entityVersions[id] = Math.Max(accepted.Version, entityVersions[id] + 1);
                    }
                    else
                    {
                        updatesFailed++;
                    }
                }
            }

            await ctx.CompleteStepAsync(4,
                $"Created {newIds.Count} new ({newFailed} failed), applied {updatesApplied} updates ({updatesFailed} failed)",
                new Dictionary<string, object>
                {
                    ["newIds"] = newIds,
                    ["newCreated"] = newIds.Count,
                    ["newFailed"] = newFailed,
                    ["updatesApplied"] = updatesApplied,
                    ["updatesFailed"] = updatesFailed,
                    ["entityVersions"] = entityVersions
                });

            // Step 5: Verify stale/missing data per chaos-affected target
            await ctx.BeginStepAsync(5, $"Verifying staleness in {verifiable.Count} verifiable target(s): [{string.Join(", ", verifiable)}]...");
            await Task.Delay(TimeSpan.FromSeconds(3), ct);

            var perTargetStats = new Dictionary<string, object>();
            foreach (string target in verifiable)
            {
                int staleCount = 0, baselineMissing = 0, newMissing = 0;

                foreach (Guid id in baselineIds)
                {
                    int expectedVersion = entityVersions[id];
                    int? actualVersion = await versionChecker.GetEntityVersionAsync(target, id, ct);
                    if (actualVersion is null)
                    {
                        baselineMissing++;
                        stalePairs.Add((id, target));
                    }
                    else if (actualVersion.Value < expectedVersion)
                    {
                        staleCount++;
                        stalePairs.Add((id, target));
                    }
                }

                foreach (Guid id in newIds)
                {
                    int? actualVersion = await versionChecker.GetEntityVersionAsync(target, id, ct);
                    if (actualVersion is null)
                    {
                        newMissing++;
                        stalePairs.Add((id, target));
                    }
                }

                perTargetStats[target] = new { stale = staleCount, baselineMissing, newMissing };
            }

            if (stalePairs.Count == 0)
            {
                await ctx.FailStepAsync(5, "Chaos did not produce any verifiable inconsistencies to heal");
                await ctx.FinishAsync(false, "No inconsistencies were observed in verifiable targets after chaos activation");
                return;
            }

            int uniqueEntities = stalePairs.Select(p => p.EntityId).Distinct().Count();
            await ctx.CompleteStepAsync(5,
                $"Found {stalePairs.Count} stale (entity,target) pairs across {uniqueEntities} entities",
                new Dictionary<string, object>
                {
                    ["stalePairsCount"] = stalePairs.Count,
                    ["uniqueEntitiesCount"] = uniqueEntities,
                    ["perTarget"] = perTargetStats,
                    ["unverifiableTargets"] = unverifiable
                });

            // Step 6: Deactivate proxy chaos — with optional CH-only extra update for version gaps
            await ctx.BeginStepAsync(6, "Deactivating proxy chaos...");
            if (clickhouseTargeted)
            {
                // Deactivate chaos ONLY on clickhouse first, so it receives new events
                await chaosManager.DeactivateOnAsync(["clickhouse"], proxyScenario, ct);

                // Push one extra update per baseline entity — CH will get this event but missed previous ones
                int chExtraUpdates = 0;
                foreach (Guid id in baselineIds)
                {
                    string extraName = fakeSample.NewName();
                    var accepted = await commandsClient.ChangeSampleNameAsync(id, extraName, ct, entityVersions[id]);
                    if (accepted is not null)
                    {
                        chExtraUpdates++;
                        entityVersions[id] = Math.Max(accepted.Version, entityVersions[id] + 1);
                    }
                }
                logger.LogInformation("[Run {RunId}] Pushed {Count} extra updates with CH chaos lifted (version gaps in CH)", ctx.RunId, chExtraUpdates);

                // Wait a moment for CH to receive the new events
                await Task.Delay(TimeSpan.FromSeconds(3), ct);

                // Now deactivate chaos on remaining targets
                var remainingTargets = targetPlugins.Where(t => !t.Equals("clickhouse", StringComparison.OrdinalIgnoreCase)).ToList();
                if (remainingTargets.Count > 0)
                    await chaosManager.DeactivateOnAsync(remainingTargets, proxyScenario, ct);

                await ctx.CompleteStepAsync(6, $"Proxy chaos deactivated (CH-first with {chExtraUpdates} extra updates for version gaps)");
            }
            else
            {
                await chaosManager.DeactivateOnAsync(targetPlugins, proxyScenario, ct);
                await ctx.CompleteStepAsync(6, "Proxy chaos deactivated");
            }

            // Step 7: Resume reconciler
            await ctx.BeginStepAsync(7, "Resuming reconciler checks and dispatching...");
            await reconciler.DeactivateAsync(ChaosScenarios.Reconciler.PauseChecks, ct);
            await reconciler.DeactivateAsync(ChaosScenarios.Reconciler.PauseDispatching, ct);
            await ctx.CompleteStepAsync(7, "Reconciler resumed — self-healing should begin");

            // Step 8: Wait for self-healing — poll each (entity, target) pair
            await ctx.BeginStepAsync(8, $"Polling {stalePairs.Count} (entity,target) pairs for healing (timeout: {selfHealTimeoutSec}s)...");
            var deadline = DateTimeOffset.UtcNow.AddSeconds(selfHealTimeoutSec);
            int pollCount = 0;

            while (DateTimeOffset.UtcNow < deadline && healedPairs.Count < stalePairs.Count)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(TimeSpan.FromSeconds(pollIntervalSec), ct);
                pollCount++;

                foreach (var (entityId, target) in stalePairs)
                {
                    if (healedPairs.Contains((entityId, target))) continue;

                    bool healed = await versionChecker.IsEntityHealedAsync(target, entityId, entityVersions[entityId], ct);
                    if (healed)
                        healedPairs.Add((entityId, target));
                }

                await ctx.UpdateStepAsync(8,
                    $"Poll #{pollCount}: {healedPairs.Count}/{stalePairs.Count} (entity,target) pairs healed",
                    new Dictionary<string, object>
                    {
                        ["healedCount"] = healedPairs.Count,
                        ["totalCount"] = stalePairs.Count,
                        ["pollCount"] = pollCount
                    });
            }

            bool allHealed = healedPairs.Count == stalePairs.Count;
            var unhealedPairs = stalePairs.Except(healedPairs).ToList();
            await ctx.CompleteStepAsync(8,
                allHealed
                    ? $"All {stalePairs.Count} (entity,target) pairs healed after {pollCount} polls"
                    : $"Timeout: {healedPairs.Count}/{stalePairs.Count} pairs healed after {pollCount} polls",
                new Dictionary<string, object>
                {
                    ["healedCount"] = healedPairs.Count,
                    ["totalCount"] = stalePairs.Count,
                    ["pollCount"] = pollCount,
                    ["timedOut"] = !allHealed,
                    ["unhealedPairs"] = unhealedPairs.Select(p => new { p.EntityId, p.Target }).ToList()
                });

            // Step 9: Report results
            await ctx.BeginStepAsync(9, "Compiling final report...");
            await ctx.CompleteStepAsync(9, allHealed ? "All entities recovered successfully" : "Some entities did not recover within timeout",
                new Dictionary<string, object>
                {
                    ["success"] = allHealed,
                    ["baselineEntities"] = baselineIds.Count,
                    ["newEntities"] = newIds.Count,
                    ["totalEntities"] = baselineIds.Count + newIds.Count,
                    ["stalePairs"] = stalePairs.Count,
                    ["healedPairs"] = healedPairs.Count,
                    ["proxyScenario"] = proxyScenario,
                    ["targetPlugins"] = targetPlugins,
                    ["verifiedTargets"] = verifiable,
                    ["totalPolls"] = pollCount
                });

            await ctx.FinishAsync(allHealed, allHealed ? null : $"{stalePairs.Count - healedPairs.Count} (entity,target) pairs failed to self-heal within {selfHealTimeoutSec}s");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            wasCancelled = true;
            logger.LogInformation("[Run {RunId}] Scenario cancellation requested", ctx.RunId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Run {RunId}] Scenario failed with exception", ctx.RunId);
            await ctx.FinishAsync(false, ex.Message);
        }
        finally
        {
            try
            {
                var allTargets = chaosManager.AllPluginNames.Append("reconciler");
                await chaosManager.DeactivateAllOnAsync(allTargets, CancellationToken.None);
                logger.LogInformation("[Run {RunId}] Chaos cleanup completed", ctx.RunId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[Run {RunId}] Failed to cleanup chaos during finally block", ctx.RunId);
            }

            if (wasCancelled)
                await ctx.CancelAsync("Scenario was cancelled");
        }
    }
}
