namespace ECK1.Integration.Plugin.Abstractions;

public class ProxyConfig
{
    public static string Section => "Proxy";
    public string Plugin { get; set; }
    public string PluginsDir { get; set; }
}
