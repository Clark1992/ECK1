using Microsoft.Extensions.Configuration;

namespace ECK1.Integration.Common;

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
                EventsTopic = contractSection[nameof(IntegrationConfigEntry.EventsTopic)],
                RecordTopic = contractSection[nameof(IntegrationConfigEntry.RecordTopic)],
            };

            var pluginsSection = contractSection.GetSection("Plugins");

            if (!string.IsNullOrEmpty(targetPlugin))
            {
                entry.PluginConfig = pluginsSection.GetChildren().FirstOrDefault(x =>
                    x.Key.Equals(targetPlugin, StringComparison.OrdinalIgnoreCase));
            }

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
                [nameof(IntegrationConfigEntry.EventsTopic)] = entry.EventsTopic,
                [nameof(IntegrationConfigEntry.RecordTopic)] = entry.RecordTopic
            };

            if (entry.PluginConfig != null)
            {
                entryDict[nameof(IntegrationConfigEntry.PluginConfig)] = entry.PluginConfig.ToSerializableObject();
            }

            result[key] = entryDict;
        }

        return result;
    }
}
