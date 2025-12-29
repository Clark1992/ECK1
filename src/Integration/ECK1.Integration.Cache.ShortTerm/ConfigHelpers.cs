namespace ECK1.Integration.Cache.ShortTerm;

public static class ConfigHelpers
{
    public static IntegrationConfig LoadConfig(IConfiguration configuration, string targetPlugin = null)
    {
        var section = configuration.GetSection(IntegrationConfig.Section);

        var result = new IntegrationConfig();

        foreach (var contractSection in section.GetChildren())
        {
            var entry = new IntegrationConfigEntry
            {
                RecordTopic = contractSection[nameof(IntegrationConfigEntry.RecordTopic)],
            };

            result[contractSection.Key] = entry;
        }

        return result;
    }

    public static object ToSerializableObject(this IConfigurationSection section)
    {
        var children = section.GetChildren().ToList();

        // Leaf
        if (!children.Any())
            return section.Value!;

        // Array?
        if (children.All(c => int.TryParse(c.Key, out _)))
        {
            return children
                .OrderBy(c => int.Parse(c.Key))
                .Select(c => c.ToSerializableObject())
                .ToList();
        }

        // Object
        var dict = new Dictionary<string, object>();
        foreach (var child in children)
            dict[child.Key] = child.ToSerializableObject();

        return dict;
    }

    public static Dictionary<string, object> Normalize(IntegrationConfig config)
    {
        var result = new Dictionary<string, object>();

        foreach (var (key, entry) in config)
        {
            var entryDict = new Dictionary<string, object>
            {
                [nameof(IntegrationConfigEntry.RecordTopic)] = entry.RecordTopic
            };

            result[key] = entryDict;
        }

        return result;
    }
}
