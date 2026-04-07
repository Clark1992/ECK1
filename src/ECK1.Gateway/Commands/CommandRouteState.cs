using ECK1.AsyncApi.Document;

namespace ECK1.Gateway.Commands;

/// <summary>
/// Thread-safe state for discovered command routes.
/// Maps "METHOD:/{serviceName}/route" → command descriptor + topic.
/// </summary>
public class CommandRouteState
{
    private volatile IReadOnlyDictionary<string, CommandRouteEntry> _routes =
        new Dictionary<string, CommandRouteEntry>();

    public IReadOnlyDictionary<string, CommandRouteEntry> Routes => _routes;

    /// <summary>
    /// Raised after routes are replaced so subscribers (e.g. DynamicCommandEndpointDataSource)
    /// can rebuild their endpoint collections.
    /// </summary>
    public event Action RoutesChanged;

    public void UpdateRoutes(IReadOnlyDictionary<string, CommandRouteEntry> routes)
    {
        _routes = routes;
        RoutesChanged?.Invoke();
    }
}

public class CommandRouteEntry
{
    public string ServiceName { get; init; } = string.Empty;
    public string Method { get; init; } = "POST";
    public string Route { get; init; } = string.Empty;
    public string FullRoutePattern { get; init; } = string.Empty;
    public string Topic { get; init; } = string.Empty;
    public string KeyProperty { get; init; }
    public string CommandName { get; init; } = string.Empty;
    public List<string> RequiredPermissions { get; init; } = [];
    public List<AsyncApiPropertyDescriptor> Properties { get; init; } = [];
}
