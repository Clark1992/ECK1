using System.Text.Json;

namespace ECK1.FailedViewRebuilder;

public static class ConfigHelpers
{
    public static IConfigurationBuilder AddFailureHandlingManifest(
        this IConfigurationBuilder builder,
        string directoryPath = "/config")
    {
        builder.Add(new ManifestDirectoryConfigurationSource
        {
            DirectoryPath = directoryPath,
            SectionName = FailureHandlingConfig.Section,
        });

        return builder;
    }

    public static FailureHandlingConfig SetupFailureHandling(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var config = LoadConfig(configuration);
        services.AddSingleton(config);
        return config;
    }

    public static void MapFailureHandlingEndpoints(
        this WebApplication app,
        Action<string> onEntityType)
    {
        var config = app.Services.GetRequiredService<FailureHandlingConfig>();
        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("FailureHandling");

        var normalized = Normalize(config);
        var json = JsonSerializer.Serialize(normalized, new JsonSerializerOptions { WriteIndented = true });
        logger.LogInformation("Starting with FailureHandlingConfig:\n{Config}", json);

        foreach (var entityType in config.Keys)
            onEntityType(entityType);
    }

    private static FailureHandlingConfig LoadConfig(IConfiguration configuration)
    {
        var section = configuration.GetSection(FailureHandlingConfig.Section);
        var result = new FailureHandlingConfig();

        foreach (var entitySection in section.GetChildren())
        {
            var entry = new FailureHandlingConfigEntry
            {
                RebuildRequestTopic = entitySection[nameof(FailureHandlingConfigEntry.RebuildRequestTopic)],
            };

            result[entitySection.Key] = entry;
        }

        return result;
    }

    private static Dictionary<string, object> Normalize(FailureHandlingConfig config)
    {
        var result = new Dictionary<string, object>();

        foreach (var (key, entry) in config)
        {
            var entryDict = new Dictionary<string, object>
            {
                [nameof(FailureHandlingConfigEntry.RebuildRequestTopic)] = entry.RebuildRequestTopic,
            };

            result[key] = entryDict;
        }

        return result;
    }
}
