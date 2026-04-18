namespace ECK1.CommonUtils.Chaos;

public static class ChaosScenarios
{
    public static class Proxy
    {
        /// <summary>
        /// Silently drops the event without processing.
        /// Simulates "event never received" — Reconciler should detect the version gap.
        /// </summary>
        public const string DropEvent = "proxy.drop-event";

        /// <summary>
        /// Throws before cache fetch, causing a DLQ message.
        /// Simulates cache unavailability — FailedViewRebuilder should pick up and rebuild.
        /// </summary>
        public const string CacheMiss = "proxy.cache-miss";

        /// <summary>
        /// Throws before plugin push, causing a DLQ message.
        /// Simulates target storage write failure — FailedViewRebuilder should pick up and rebuild.
        /// </summary>
        public const string PushFail = "proxy.push-fail";

        /// <summary>
        /// Skips the plugin push but commits the Kafka offset.
        /// Simulates "reported success but didn't write" — Reconciler should detect the version mismatch.
        /// </summary>
        public const string PushNoop = "proxy.push-noop";
    }

    public static class Reconciler
    {
        /// <summary>
        /// Pauses reconciliation checks — the ReconciliationCheckService skips its cycle.
        /// </summary>
        public const string PauseChecks = "reconciler.pause-checks";

        /// <summary>
        /// Pauses rebuild dispatching — the RebuildDispatchService skips its cycle.
        /// </summary>
        public const string PauseDispatching = "reconciler.pause-dispatching";
    }

    public static IReadOnlyList<ScenarioInfo> All { get; } =
    [
        new(Proxy.DropEvent,
            "Integration Proxy: drop event",
            "Silently drops the event without processing. Reconciler should detect the version gap and trigger rebuild."),

        new(Proxy.CacheMiss,
            "Integration Proxy: force cache miss",
            "Throws before cache fetch, producing a DLQ message. FailedViewRebuilder should pick up and send a rebuild request."),

        new(Proxy.PushFail,
            "Integration Proxy: force push failure",
            "Throws before plugin push, producing a DLQ message. FailedViewRebuilder should pick up and send a rebuild request."),

        new(Proxy.PushNoop,
            "Integration Proxy: silent push skip",
            "Skips the plugin push but commits the offset. Reconciler should detect version mismatch and trigger rebuild."),

        new(Reconciler.PauseChecks,
            "Reconciler: pause consistency checks",
            "Pauses the periodic reconciliation check loop. Useful for chaos scenarios that need to prevent self-healing until ready."),

        new(Reconciler.PauseDispatching,
            "Reconciler: pause rebuild dispatching",
            "Pauses the periodic rebuild dispatch loop. Useful for chaos scenarios that need to delay rebuild requests."),
    ];
}

public record ScenarioInfo(string Id, string Name, string Description);
