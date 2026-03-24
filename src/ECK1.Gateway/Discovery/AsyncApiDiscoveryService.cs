using System.Text.Json;
using ECK1.AsyncApi.Document;

namespace ECK1.Gateway.Discovery;

public interface IAsyncApiDiscoveryService
{
    Task<AsyncApiDocument> FetchAsyncApiAsync(DiscoveredService service, CancellationToken ct);
}

public class AsyncApiDiscoveryService : IAsyncApiDiscoveryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GatewayConfig _config;
    private readonly ILogger<AsyncApiDiscoveryService> _logger;

    public AsyncApiDiscoveryService(
        IHttpClientFactory httpClientFactory,
        Microsoft.Extensions.Options.IOptions<GatewayConfig> config,
        ILogger<AsyncApiDiscoveryService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config.Value;
        _logger = logger;
    }

    public async Task<AsyncApiDocument> FetchAsyncApiAsync(DiscoveredService service, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("ServiceDiscovery");
        var url = $"{service.BaseUrl}{_config.AsyncApiPath}";

        try
        {
            var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch async API from {Service} at {Url}: {StatusCode}",
                    service.Name, url, response.StatusCode);
                return null;
            }

            var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonSerializer.DeserializeAsync<AsyncApiDocument>(stream, JsonOptions, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching async API from {Service} at {Url}", service.Name, url);
            return null;
        }
    }
}
