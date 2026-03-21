using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECK1.Integration.Config;

public class IntegrationConfig : Dictionary<string, IntegrationConfigEntry>
{
    public static string Section => nameof(IntegrationConfig);
}

public class IntegrationConfigEntry
{
    public string RecordType { get; set; } = "";
    public string EventsTopic { get; set; } = "";
    public string RecordTopic { get; set; } = "";
    public string EntityType { get; set; } = "";
    public IConfigurationSection? PluginConfig { get; set; }
}

public static class ConfigHelpers
{
    /// <summary>
    /// Adds a configuration source that reads mounted ConfigMap YAML files from a directory.
    /// </summary>
    public static IConfigurationBuilder AddIntegrationManifest(
        this IConfigurationBuilder builder,
        string directoryPath = "/config",
        string sectionName = "IntegrationConfig")
    {
        builder.Add(new ManifestDirectoryConfigurationSource
        {
            DirectoryPath = directoryPath,
            SectionName = sectionName,
        });

        return builder;
    }

    /// <summary>
    /// Loads integration manifest, registers <see cref="IntegrationConfig"/> as singleton,
    /// and iterates entries invoking callbacks for ThinEvent and FullRecord producer setup.
    /// </summary>
    public static IntegrationConfig SetupEventIntegrations(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<string, string> setupThinEvent,
        Action<Type, string> setupFullRecord)
    {
        var integrationConfig = LoadConfig(configuration);
        services.AddSingleton(integrationConfig);

        foreach (var (recordTypeName, entry) in integrationConfig)
        {
            setupThinEvent(entry.EntityType, entry.EventsTopic);

            var recordType = TypeResolver.ResolveType(recordTypeName);
            if (recordType is not null)
                setupFullRecord(recordType, entry.RecordTopic);
        }

        return integrationConfig;
    }

    /// <summary>
    /// Loads integration manifest entries from <see cref="IntegrationConfig.Section"/> in IConfiguration.
    /// Optionally filters plugin config by <paramref name="targetPlugin"/>.
    /// </summary>
    public static IntegrationConfig LoadConfig(IConfiguration configuration, string? targetPlugin = null)
    {
        var section = configuration.GetSection(IntegrationConfig.Section);
        var result = new IntegrationConfig();

        foreach (var entitySection in section.GetChildren())
        {
            var recordType = entitySection["RecordType"];
            if (string.IsNullOrEmpty(recordType)) continue;

            var entry = new IntegrationConfigEntry
            {
                RecordType = recordType,
                EventsTopic = entitySection[nameof(IntegrationConfigEntry.EventsTopic)] ?? "",
                RecordTopic = entitySection[nameof(IntegrationConfigEntry.RecordTopic)] ?? "",
                EntityType = entitySection.Key,
            };

            if (!string.IsNullOrEmpty(targetPlugin))
            {
                var pluginsSection = entitySection.GetSection("Plugins");
                entry.PluginConfig = pluginsSection.GetChildren()
                    .FirstOrDefault(x => x.Key.Equals(targetPlugin, StringComparison.OrdinalIgnoreCase));
            }

            result[recordType] = entry;
        }

        return result;
    }

    public static object ToSerializableObject(this IConfigurationSection section)
    {
        var children = section.GetChildren().ToList();

        if (children.Count == 0)
            return section.Value!;

        if (children.All(c => int.TryParse(c.Key, out _)))
        {
            return children
                .OrderBy(c => int.Parse(c.Key))
                .Select(c => c.ToSerializableObject())
                .ToList();
        }

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
                [nameof(IntegrationConfigEntry.RecordTopic)] = entry.RecordTopic,
                [nameof(IntegrationConfigEntry.EntityType)] = entry.EntityType,
            };

            if (entry.PluginConfig is not null)
            {
                entryDict[nameof(IntegrationConfigEntry.PluginConfig)] = entry.PluginConfig.ToSerializableObject();
            }

            result[key] = entryDict;
        }

        return result;
    }
}
