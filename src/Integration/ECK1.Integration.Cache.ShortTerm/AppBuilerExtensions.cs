using ECK1.CommonUtils.Secrets.Doppler;
using ECK1.CommonUtils.Secrets.K8s;
using ECK1.Integration.Plugin.Abstractions;
using static ECK1.Integration.Plugin.Abstractions.ConfigHelpers;

namespace ECK1.Integration.Cache.ShortTerm;

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
}
