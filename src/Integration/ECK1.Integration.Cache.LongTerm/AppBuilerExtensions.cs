using ECK1.CommonUtils.Secrets.Doppler;
using ECK1.CommonUtils.Secrets.K8s;

namespace ECK1.Integration.Cache.LongTerm;

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
