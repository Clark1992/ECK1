using ECK1.AsyncApi.Document;
using ECK1.Gateway.Commands;
using ECK1.Gateway.Discovery;
using ECK1.Gateway.Proxy;
using Microsoft.Extensions.Options;

namespace ECK1.Gateway.Workers;

/// <summary>
/// Background service that periodically discovers services from Kubernetes,
/// fetches their OpenAPI and AsyncAPI documents, and updates the gateway's
/// routing state (YARP proxy routes + command endpoints).
/// </summary>
public class GatewayRefreshWorker(
    IServiceDiscovery serviceDiscovery,
    ISwaggerDiscoveryService swaggerDiscovery,
    IAsyncApiDiscoveryService asyncApiDiscovery,
    ServiceRouteState routeState,
    DynamicYarpConfigProvider yarpProvider,
    CommandRouteState commandState,
    IOptions<GatewayConfig> config,
    ILogger<GatewayRefreshWorker> logger) : BackgroundService
{
    private readonly GatewayConfig _config = config.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Gateway refresh worker started (interval: {Interval}s)",
            _config.RefreshIntervalSeconds);

        // Initial delay to let services start
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error during gateway refresh cycle");
            }

            await Task.Delay(TimeSpan.FromSeconds(_config.RefreshIntervalSeconds), stoppingToken);
        }
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        logger.LogDebug("Starting gateway refresh cycle");

        // 1. Discover services
        var discoveredServices = await serviceDiscovery.DiscoverServicesAsync(ct);
        logger.LogInformation("Discovered {Count} services", discoveredServices.Count);

        var snapshots = discoveredServices.Select(s =>
            new ServiceSnapshot(s.Name, s.BaseUrl, s.ExposesApi, s.ExposesAsyncApi)).ToList();
        routeState.UpdateServices(snapshots);

        // Track current service names for cleanup
        var currentServiceNames = new HashSet<string>(discoveredServices.Select(s => s.Name));

        // Remove stale services
        foreach (var name in routeState.SwaggerDocs.Keys.Except(currentServiceNames))
        {
            routeState.RemoveService(name);
            logger.LogInformation("Removed stale service: {Service}", name);
        }

        // 2. Fetch swagger docs in parallel for API services
        var apiServices = discoveredServices.Where(s => s.ExposesApi).ToList();
        var swaggerTasks = apiServices.Select(async svc =>
        {
            var doc = await swaggerDiscovery.FetchSwaggerAsync(svc, ct);
            if (doc is not null)
            {
                routeState.SetSwaggerDoc(svc.Name, doc);
                logger.LogDebug("Updated swagger for {Service}", svc.Name);
            }
        });
        await Task.WhenAll(swaggerTasks);

        // 3. Fetch async API docs in parallel for async-api services
        var asyncServices = discoveredServices.Where(s => s.ExposesAsyncApi).ToList();
        var asyncTasks = asyncServices.Select(async svc =>
        {
            var doc = await asyncApiDiscovery.FetchAsyncApiAsync(svc, ct);
            if (doc is not null)
            {
                routeState.SetAsyncApiDoc(svc.Name, doc);
                logger.LogDebug("Updated async API for {Service}", svc.Name);
            }
        });
        await Task.WhenAll(asyncTasks);

        // 4. Update YARP proxy routes
        yarpProvider.Update();

        // 5. Update command routes
        RebuildCommandRoutes();

        logger.LogDebug("Gateway refresh cycle completed");
    }

    private void RebuildCommandRoutes()
    {
        var routes = new Dictionary<string, CommandRouteEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var (serviceName, asyncDoc) in routeState.AsyncApiDocs)
        {
            foreach (var command in asyncDoc.Commands)
            {
                var fullRoute = $"/{serviceName}{command.Route}";
                var routeKey = $"{command.Method}:{fullRoute}";

                routes[routeKey] = new CommandRouteEntry
                {
                    ServiceName = serviceName,
                    Method = command.Method,
                    Route = command.Route,
                    FullRoutePattern = fullRoute,
                    Topic = command.Topic,
                    KeyProperty = command.KeyProperty,
                    CommandName = command.Name,
                    Properties = command.Properties
                };
            }
        }

        commandState.UpdateRoutes(routes);
        logger.LogInformation("Updated command routes: {Count} endpoints", routes.Count);
    }
}
