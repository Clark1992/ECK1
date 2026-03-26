using System.Text.Json;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace ECK1.Gateway.Proxy;

/// <summary>
/// Dynamic YARP configuration provider that generates routes and clusters
/// from discovered services and their OpenAPI paths.
/// </summary>
public class DynamicYarpConfigProvider : IProxyConfigProvider, IDisposable
{
    private readonly ServiceRouteState _state;
    private readonly ILogger<DynamicYarpConfigProvider> _logger;
    private volatile YarpSnapshot _snapshot;
    private CancellationTokenSource _cts = new();

    public DynamicYarpConfigProvider(
        ServiceRouteState state,
        ILogger<DynamicYarpConfigProvider> logger)
    {
        _state = state;
        _logger = logger;
        _snapshot = new YarpSnapshot([], [], new CancellationChangeToken(_cts.Token));
    }

    public IProxyConfig GetConfig() => _snapshot;

    /// <summary>
    /// Rebuilds YARP routes and clusters from the current service state.
    /// </summary>
    public void Update()
    {
        try
        {
            var routes = new List<RouteConfig>();
            var clusters = new List<ClusterConfig>();

            foreach (var service in _state.Services)
            {
                if (!service.ExposesApi) continue;

                var clusterId = $"cluster-{service.Name}";

                clusters.Add(new ClusterConfig
                {
                    ClusterId = clusterId,
                    Destinations = new Dictionary<string, DestinationConfig>
                    {
                        [service.Name] = new DestinationConfig { Address = service.BaseUrl }
                    },
                    HttpRequest = new Yarp.ReverseProxy.Forwarder.ForwarderRequestConfig
                    {
                        ActivityTimeout = TimeSpan.FromSeconds(30)
                    }
                });

                // If we have swagger, create specific routes per path
                if (_state.SwaggerDocs.TryGetValue(service.Name, out var swaggerDoc))
                {
                    AddSwaggerRoutes(routes, swaggerDoc, service.Name, clusterId);
                }
                else
                {
                    // Fallback: catch-all route for the service prefix
                    routes.Add(new RouteConfig
                    {
                        RouteId = $"route-{service.Name}-catchall",
                        ClusterId = clusterId,
                        Match = new RouteMatch
                        {
                            Path = $"/{service.Name}/{{**remainder}}"
                        },
                        Transforms =
                        [
                            new Dictionary<string, string>
                            {
                                ["PathRemovePrefix"] = $"/{service.Name}"
                            }
                        ]
                    });
                }
            }

            var oldCts = _cts;
            _cts = new CancellationTokenSource();
            _snapshot = new YarpSnapshot(routes, clusters, new CancellationChangeToken(_cts.Token));

            oldCts.Cancel();
            oldCts.Dispose();

            _logger.LogDebug("YARP config updated: {RouteCount} routes, {ClusterCount} clusters",
                routes.Count, clusters.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update YARP configuration");
        }
    }

    private static void AddSwaggerRoutes(
        List<RouteConfig> routes,
        JsonDocument swagger,
        string serviceName,
        string clusterId)
    {
        if (!swagger.RootElement.TryGetProperty("paths", out var paths))
            return;

        // Create a single catch-all route per service that strips the service prefix.
        // This is simpler and more resilient than creating individual routes per path.
        routes.Add(new RouteConfig
        {
            RouteId = $"route-{serviceName}-api",
            ClusterId = clusterId,
            Match = new RouteMatch
            {
                Path = $"/{serviceName}/{{**remainder}}"
            },
            Transforms =
            [
                new Dictionary<string, string>
                {
                    ["PathRemovePrefix"] = $"/{serviceName}"
                }
            ],
            Order = 100 // lower priority than command routes
        });
    }

    public void Dispose() => _cts?.Dispose();

    private sealed class YarpSnapshot(
        IReadOnlyList<RouteConfig> routes,
        IReadOnlyList<ClusterConfig> clusters,
        IChangeToken changeToken) : IProxyConfig
    {
        public IReadOnlyList<RouteConfig> Routes { get; } = routes;
        public IReadOnlyList<ClusterConfig> Clusters { get; } = clusters;
        public IChangeToken ChangeToken { get; } = changeToken;
    }
}
