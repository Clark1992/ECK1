using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Primitives;

namespace ECK1.Gateway.Commands;

/// <summary>
/// Dynamic endpoint data source that generates ASP.NET Core route endpoints
/// from discovered async command routes. Rebuilds endpoints whenever
/// <see cref="CommandRouteState.RoutesChanged"/> fires.
/// </summary>
public sealed class DynamicCommandEndpointDataSource : EndpointDataSource, IDisposable
{
    private readonly CommandRouteState _commandState;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DynamicCommandEndpointDataSource> _logger;
    private readonly object _lock = new();

    private IReadOnlyList<Endpoint> _endpoints = [];
    private CancellationTokenSource _cts = new();

    public DynamicCommandEndpointDataSource(
        CommandRouteState commandState,
        IServiceProvider serviceProvider,
        ILogger<DynamicCommandEndpointDataSource> logger)
    {
        _commandState = commandState;
        _serviceProvider = serviceProvider;
        _logger = logger;

        _commandState.RoutesChanged += OnRoutesChanged;
        Rebuild();
    }

    public override IReadOnlyList<Endpoint> Endpoints => _endpoints;

    public override IChangeToken GetChangeToken()
    {
        lock (_lock)
        {
            return new CancellationChangeToken(_cts.Token);
        }
    }

    private void OnRoutesChanged()
    {
        Rebuild();
    }

    private void Rebuild()
    {
        var routes = _commandState.Routes;
        var endpoints = new List<Endpoint>(routes.Count);

        foreach (var (_, entry) in routes)
        {
            var pattern = RoutePatternFactory.Parse(entry.FullRoutePattern);

            var builder = new RouteEndpointBuilder(
                requestDelegate: CreateRequestDelegate(),
                routePattern: pattern,
                order: 0)
            {
                DisplayName = $"Command: {entry.Method} {entry.FullRoutePattern} → {entry.CommandName}"
            };

            builder.Metadata.Add(entry);
            builder.Metadata.Add(new HttpMethodMetadata([entry.Method]));

            endpoints.Add(builder.Build());
        }

        // Swap endpoints and signal change token
        CancellationTokenSource oldCts;
        lock (_lock)
        {
            _endpoints = endpoints;
            oldCts = _cts;
            _cts = new CancellationTokenSource();
        }

        oldCts.Cancel();
        oldCts.Dispose();

        _logger.LogDebug(
            "Rebuilt command endpoints: {Count} routes registered", endpoints.Count);
    }

    private static RequestDelegate CreateRequestDelegate()
    {
        return async context =>
        {
            var handler = context.RequestServices.GetRequiredService<CommandEndpointHandler>();
            await handler.HandleAsync(context);
        };
    }

    public void Dispose()
    {
        _commandState.RoutesChanged -= OnRoutesChanged;
        _cts.Dispose();
    }
}
