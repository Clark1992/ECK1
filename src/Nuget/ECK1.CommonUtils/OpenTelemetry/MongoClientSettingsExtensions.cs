using MongoDB.Driver;
using MongoDB.Driver.Core.Extensions.DiagnosticSources;

namespace ECK1.CommonUtils.OpenTelemetry;

public static class MongoClientSettingsExtensions
{
    public static MongoClientSettings AddOpenTelemetryInstrumentation(this MongoClientSettings settings)
    {
        var existingConfigurator = settings.ClusterConfigurator;
        settings.ClusterConfigurator = builder =>
        {
            existingConfigurator?.Invoke(builder);
            builder.Subscribe(new DiagnosticsActivityEventSubscriber());
        };

        return settings;
    }
}