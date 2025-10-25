using Microsoft.Extensions.Configuration;

namespace ECK1.CommonUtils.Secrets.K8s;

public static class K8sConfigurationExtensions
{
    public static IConfigurationBuilder AddK8sSecrets(this IConfigurationBuilder configuration) =>
        configuration.AddKeyPerFile("/etc/secrets", true);
}
