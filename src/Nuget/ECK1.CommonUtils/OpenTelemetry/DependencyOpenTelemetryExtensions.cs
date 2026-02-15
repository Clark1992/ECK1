using OpenTelemetry.Trace;
using MongoDB.Driver.Core.Extensions.DiagnosticSources;

namespace ECK1.CommonUtils.OpenTelemetry;

public static class DependencyOpenTelemetryExtensions
{
    public const string ElasticsearchActivitySourceName = "Elastic.Transport";
    public const string NatsActivitySourceName = "Nats";

    public static TracerProviderBuilder AddMongoInstrumentation(this TracerProviderBuilder tracing) =>
        tracing.AddSource(DiagnosticsActivityEventSubscriber.ActivitySourceName);

    public static TracerProviderBuilder AddElasticsearchInstrumentation(this TracerProviderBuilder tracing) =>
        tracing.AddSource(ElasticsearchActivitySourceName);

    public static TracerProviderBuilder AddNatsInstrumentation(this TracerProviderBuilder tracing) =>
        tracing.AddSource(NatsActivitySourceName);
}
