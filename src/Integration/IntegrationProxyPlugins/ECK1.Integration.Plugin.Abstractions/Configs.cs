using Microsoft.Extensions.Configuration;

namespace ECK1.Integration.Plugin.Abstractions;

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

public class ProxyConfig
{
    public static string Section => "Proxy";
    public string Plugin { get; set; }
    public string PluginsDir { get; set; }
}
