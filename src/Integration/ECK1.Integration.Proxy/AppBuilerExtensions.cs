using ECK1.CommonUtils.Secrets.Doppler;
using ECK1.CommonUtils.Secrets.K8s;
using ECK1.Integration.Common;
using ECK1.Integration.Plugin.Abstractions;

using static ECK1.Integration.Common.ConfigHelpers;

namespace ECK1.Integration.Proxy;

public static class AppBuilerExtensions
{
    public static void AddConfigSources(this WebApplicationBuilder builder)
    {
        builder.Configuration.AddK8sSecrets();

#if DEBUG
        builder.Configuration.AddUserSecrets<Program>();
#endif

        builder.Configuration.AddDopplerSecrets();
        builder.Configuration.AddJsonFile("/config/merged.json", optional: true);
    }

    public static ProxyConfig GetProxyType(this WebApplicationBuilder builder)
    {
        var proxyConfigSection = builder.Configuration
            .GetSection(ProxyConfig.Section);

        var proxyConfig = proxyConfigSection.Get<ProxyConfig>();

        if (!Constants.Plugins.Contains(proxyConfig.Plugin, StringComparer.OrdinalIgnoreCase))
        {
            throw new Exception(
                $"proxyConfig.Plugin should be one of [{string.Join(',', Constants.Plugins)}]. Service cannot start without it correctly specified.");
        }

        return proxyConfig;
    }

    public static IntegrationConfig GetIntegrationConfig(this WebApplicationBuilder builder, string plugin) =>
        LoadConfig(builder.Configuration, plugin);
}
