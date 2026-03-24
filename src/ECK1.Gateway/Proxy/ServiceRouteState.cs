using System.Collections.Concurrent;
using System.Text.Json;
using ECK1.AsyncApi.Document;

namespace ECK1.Gateway.Proxy;

/// <summary>
/// Thread-safe in-memory state holder for discovered services, routes, and swagger docs.
/// </summary>
public class ServiceRouteState
{
    private volatile IReadOnlyList<ServiceSnapshot> _services = [];
    private readonly ConcurrentDictionary<string, JsonDocument> _swaggerDocs = new();
    private readonly ConcurrentDictionary<string, AsyncApiDocument> _asyncApiDocs = new();

    public IReadOnlyList<ServiceSnapshot> Services => _services;
    public IReadOnlyDictionary<string, JsonDocument> SwaggerDocs => _swaggerDocs;
    public IReadOnlyDictionary<string, AsyncApiDocument> AsyncApiDocs => _asyncApiDocs;

    public void UpdateServices(IReadOnlyList<ServiceSnapshot> services)
    {
        _services = services;
    }

    public void SetSwaggerDoc(string serviceName, JsonDocument doc)
    {
        if (_swaggerDocs.TryGetValue(serviceName, out var old))
            old.Dispose();
        _swaggerDocs[serviceName] = doc;
    }

    public void SetAsyncApiDoc(string serviceName, AsyncApiDocument doc)
    {
        _asyncApiDocs[serviceName] = doc;
    }

    public void RemoveService(string serviceName)
    {
        if (_swaggerDocs.TryRemove(serviceName, out var doc))
            doc.Dispose();
        _asyncApiDocs.TryRemove(serviceName, out _);
    }
}

public record ServiceSnapshot(
    string Name,
    string BaseUrl,
    bool ExposesApi,
    bool ExposesAsyncApi);
