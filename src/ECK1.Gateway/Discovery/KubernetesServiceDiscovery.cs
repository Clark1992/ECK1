using k8s;
using k8s.Models;
using Microsoft.Extensions.Options;

namespace ECK1.Gateway.Discovery;

public interface IServiceDiscovery
{
    Task<IReadOnlyList<DiscoveredService>> DiscoverServicesAsync(CancellationToken ct);
}

public class KubernetesServiceDiscovery(
    IKubernetes client,
    IOptions<GatewayConfig> config,
    ILogger<KubernetesServiceDiscovery> logger) : IServiceDiscovery
{
    private const string ExposeApiLabel = "expose-api";
    private const string ExposeAsyncApiLabel = "expose-async-api";
    private readonly GatewayConfig _config = config.Value;

    public async Task<IReadOnlyList<DiscoveredService>> DiscoverServicesAsync(CancellationToken ct)
    {
        var services = new List<DiscoveredService>();

        try
        {
            // Fetch services with either expose label
            var labelSelector = $"{ExposeApiLabel}=true";
            var apiServices = await client.CoreV1.ListNamespacedServiceAsync(
                _config.Namespace, labelSelector: labelSelector, cancellationToken: ct);

            foreach (var svc in apiServices.Items)
            {
                var discovered = MapService(svc);
                if (discovered is not null)
                    services.Add(discovered);
            }

            // Fetch async-api services that might not have expose-api
            var asyncLabelSelector = $"{ExposeAsyncApiLabel}=true";
            var asyncServices = await client.CoreV1.ListNamespacedServiceAsync(
                _config.Namespace, labelSelector: asyncLabelSelector, cancellationToken: ct);

            foreach (var svc in asyncServices.Items)
            {
                if (services.Any(s => s.Name == svc.Metadata.Name))
                    continue;

                var discovered = MapService(svc);
                if (discovered is not null)
                    services.Add(discovered);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to discover services from Kubernetes");
        }

        return services;
    }

    private static DiscoveredService MapService(V1Service svc)
    {
        var name = svc.Metadata.Name;
        var labels = svc.Metadata.Labels ?? new Dictionary<string, string>();
        var port = svc.Spec.Ports?.FirstOrDefault()?.Port ?? 80;
        var baseUrl = $"http://{name}:{port}";

        labels.TryGetValue(ExposeApiLabel, out var exposeApi);
        labels.TryGetValue(ExposeAsyncApiLabel, out var exposeAsyncApi);

        return new DiscoveredService(
            name,
            baseUrl,
            string.Equals(exposeApi, "true", StringComparison.OrdinalIgnoreCase),
            string.Equals(exposeAsyncApi, "true", StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Fallback discovery for local development using static configuration.
/// </summary>
public class StaticServiceDiscovery(IOptions<GatewayConfig> config) : IServiceDiscovery
{
    private readonly GatewayConfig _config = config.Value;

    public Task<IReadOnlyList<DiscoveredService>> DiscoverServicesAsync(CancellationToken ct)
    {
        IReadOnlyList<DiscoveredService> result = [.. _config.StaticServices.Select(s =>
            new DiscoveredService(s.Name, s.Url, s.ExposeApi, s.ExposeAsyncApi))];
        return Task.FromResult(result);
    }
}
