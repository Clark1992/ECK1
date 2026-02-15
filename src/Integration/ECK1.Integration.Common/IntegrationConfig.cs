using Microsoft.Extensions.Configuration;

namespace ECK1.Integration.Common;

public class IntegrationConfig : Dictionary<string, IntegrationConfigEntry>
{
    public static string Section => nameof(IntegrationConfig);
}

public class IntegrationConfigEntry
{
    public string EventsTopic { get; set; }
    public string RecordTopic { get; set; }
    public IConfigurationSection PluginConfig { get; set; }
}
