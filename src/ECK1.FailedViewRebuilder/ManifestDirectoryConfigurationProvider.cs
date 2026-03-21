using Microsoft.Extensions.Configuration;

namespace ECK1.FailedViewRebuilder;

public sealed class ManifestDirectoryConfigurationSource : IConfigurationSource
{
    public required string DirectoryPath { get; init; }
    public string SectionName { get; init; } = "FailureHandlingConfig";

    public IConfigurationProvider Build(IConfigurationBuilder builder)
        => new ManifestDirectoryConfigurationProvider(this);
}

public sealed class ManifestDirectoryConfigurationProvider : ConfigurationProvider
{
    private readonly ManifestDirectoryConfigurationSource _source;

    public ManifestDirectoryConfigurationProvider(ManifestDirectoryConfigurationSource source)
    {
        _source = source;
    }

    public override void Load()
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(_source.DirectoryPath))
        {
            Data = data;
            return;
        }

        foreach (var filePath in Directory.GetFiles(_source.DirectoryPath))
        {
            var entityType = Path.GetFileName(filePath);
            var prefix = $"{_source.SectionName}:{entityType}";

            var yamlConfig = new ConfigurationBuilder()
                .AddYamlFile(filePath, optional: false)
                .Build();

            foreach (var kvp in yamlConfig.AsEnumerable())
            {
                if (kvp.Value is not null)
                    data[$"{prefix}:{kvp.Key}"] = kvp.Value;
            }
        }

        Data = data;
    }
}
