using System.Text.Json;

namespace ECK1.Gateway.Discovery;

public interface ISwaggerDiscoveryService
{
    Task<JsonDocument> FetchSwaggerAsync(DiscoveredService service, CancellationToken ct);
}

public class SwaggerDiscoveryService : ISwaggerDiscoveryService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GatewayConfig _config;
    private readonly ILogger<SwaggerDiscoveryService> _logger;

    public SwaggerDiscoveryService(
        IHttpClientFactory httpClientFactory,
        Microsoft.Extensions.Options.IOptions<GatewayConfig> config,
        ILogger<SwaggerDiscoveryService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config.Value;
        _logger = logger;
    }

    public async Task<JsonDocument> FetchSwaggerAsync(DiscoveredService service, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("ServiceDiscovery");
        var url = $"{service.BaseUrl}{_config.SwaggerPathTemplate}";

        try
        {
            var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch swagger from {Service} at {Url}: {StatusCode}",
                    service.Name, url, response.StatusCode);
                return null;
            }

            var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching swagger from {Service} at {Url}", service.Name, url);
            return null;
        }
    }
}
