namespace ECK1.Gateway.Discovery;

public record DiscoveredService(
    string Name,
    string BaseUrl,
    bool ExposesApi,
    bool ExposesAsyncApi);
