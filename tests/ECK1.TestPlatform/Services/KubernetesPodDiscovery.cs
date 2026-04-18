using k8s;

namespace ECK1.TestPlatform.Services;

/// <summary>
/// Resolves K8s service URLs to individual pod endpoints for chaos broadcast.
/// Falls back to the original service URL when running outside K8s.
/// </summary>
public sealed class KubernetesPodDiscovery(ILogger<KubernetesPodDiscovery> logger)
{
    private readonly bool _inCluster = KubernetesClientConfiguration.IsInCluster();
    private const string DefaultNamespace = "app-services";

    /// <summary>
    /// Returns base URLs for all pods behind the given K8s service URL.
    /// Example input: "http://eck1-integration-proxy-elasticsearch-svc"
    /// Example output: ["http://10.1.0.45:80", "http://10.1.0.46:80"]
    /// Falls back to the original URL if not running in K8s or on error.
    /// </summary>
    public async Task<IReadOnlyList<string>> ResolvePodUrlsAsync(string serviceUrl, CancellationToken ct)
    {
        if (!_inCluster)
        {
            return [serviceUrl];
        }

        try
        {
            var uri = new Uri(serviceUrl.TrimEnd('/'));
            string serviceName = uri.Host; // e.g. "eck1-integration-proxy-elasticsearch-svc"
            int port = uri.Port > 0 && uri.Port != 80 ? uri.Port : 80;

            var config = KubernetesClientConfiguration.InClusterConfig();
            using var client = new Kubernetes(config);

            var endpoints = await client.CoreV1.ReadNamespacedEndpointsAsync(serviceName, DefaultNamespace, cancellationToken: ct);
            var podUrls = new List<string>();

            if (endpoints.Subsets is not null)
            {
                foreach (var subset in endpoints.Subsets)
                {
                    var targetPort = subset.Ports?.FirstOrDefault(p => p.Port == port)?.Port ?? port;
                    if (subset.Addresses is not null)
                    {
                        foreach (var addr in subset.Addresses)
                        {
                            podUrls.Add($"http://{addr.Ip}:{targetPort}");
                        }
                    }
                }
            }

            if (podUrls.Count == 0)
            {
                logger.LogWarning("No pod endpoints found for {Service}, falling back to service URL", serviceName);
                return [serviceUrl];
            }

            logger.LogInformation("Resolved {Service} to {Count} pod(s): {Pods}", serviceName, podUrls.Count, string.Join(", ", podUrls));
            return podUrls;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to resolve pod endpoints for {ServiceUrl}, falling back to service URL", serviceUrl);
            return [serviceUrl];
        }
    }
}
