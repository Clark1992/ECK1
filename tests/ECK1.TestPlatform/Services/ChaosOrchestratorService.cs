using Microsoft.Extensions.Options;

namespace ECK1.TestPlatform.Services;

public sealed class ChaosOrchestratorService(
    IHttpClientFactory httpClientFactory,
    IOptions<ProxyServiceConfig> proxyConfig,
    ILogger<ChaosOrchestratorService> logger)
{
    public IReadOnlyList<string> AllPluginNames =>
        [.. proxyConfig.Value.Plugins.Keys];

    public async Task ActivateOnPluginsAsync(
        IEnumerable<string> plugins,
        string scenarioId,
        CancellationToken ct)
    {
        foreach (var plugin in plugins)
        {
            var client = CreateClient(plugin);
            logger.LogInformation("Activating '{ScenarioId}' on {Plugin}", scenarioId, plugin);
            await client.PostAsync($"chaos/activate/{scenarioId}", null, ct);
        }
    }

    public async Task DeactivateOnPluginsAsync(
        IEnumerable<string> plugins,
        string scenarioId,
        CancellationToken ct)
    {
        foreach (var plugin in plugins)
        {
            var client = CreateClient(plugin);
            logger.LogInformation("Deactivating '{ScenarioId}' on {Plugin}", scenarioId, plugin);
            await client.DeleteAsync($"chaos/activate/{scenarioId}", ct);
        }
    }

    public async Task DeactivateAllOnPluginsAsync(IEnumerable<string> plugins, CancellationToken ct)
    {
        foreach (var plugin in plugins)
        {
            var client = CreateClient(plugin);
            logger.LogInformation("Deactivating all chaos on {Plugin}", plugin);
            await client.DeleteAsync("chaos", ct);
        }
    }

    private HttpClient CreateClient(string plugin)
    {
        var url = proxyConfig.Value.Plugins.TryGetValue(plugin, out var baseUrl)
            ? baseUrl
            : throw new ArgumentException($"Unknown proxy plugin: '{plugin}'. Known: {string.Join(", ", proxyConfig.Value.Plugins.Keys)}");

        var client = httpClientFactory.CreateClient("chaos");
        client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(10);
        return client;
    }
}
