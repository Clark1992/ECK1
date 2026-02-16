namespace ECK1.FailedViewRebuilder;

public static class ConfigHelpers
{
    public static FailureHandlingConfig LoadConfig(IConfiguration configuration, string targetPlugin = null)
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

    public static Dictionary<string, object> Normalize(FailureHandlingConfig config)
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
