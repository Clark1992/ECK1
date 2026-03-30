using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ECK1.Integration.Common;
using ECK1.Integration.Config;
using OpenTelemetry.Trace;
using Generated = ECK1.IntegrationContracts.Kafka.IntegrationRecords.Generated;

namespace ECK1.Integration.Plugin.Abstractions;

public interface IIntergationPlugin<TEvent, TMessage>
    where TEvent: Generated.ThinEvent
{
    Task PushAsync(TEvent @event, TMessage message);
}

public interface IIntergationPluginLoader
{
    void Setup(IServiceCollection services, IConfiguration config, IntegrationConfig integrationConfig);
    void SetupTelemetry(TracerProviderBuilder tracing);
}

public record ReconciliationCheckResult(bool IsConsistent, bool RequiresFullHistoryRebuild)
{
    public static ReconciliationCheckResult Ok { get; } = new(true, false);
    public static ReconciliationCheckResult NeedsFullRebuild { get; } = new(false, true);
    public static ReconciliationCheckResult NeedsLatest { get; } = new(false, false);
}

public interface IReconciliationPlugin
{
    /// <summary>
    /// Check whether the plugin's view for the given entity is consistent with the expected version.
    /// </summary>
    Task<ReconciliationCheckResult> CheckAsync(Guid entityId, string entityType, int expectedVersion, CancellationToken ct);
}

