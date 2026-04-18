namespace ECK1.TestPlatform.Services;

public sealed class KubernetesChaosClient(
    IHttpClientFactory httpClientFactory,
    KubernetesPodDiscovery podDiscovery,
    string clientName,
    string baseUrl,
    ILogger logger) : IChaosClient
{
    public async Task ActivateAsync(string scenarioId, CancellationToken ct)
    {
        var podUrls = await podDiscovery.ResolvePodUrlsAsync(baseUrl, ct);
        foreach (var podUrl in podUrls)
        {
            var client = CreateClientForUrl(podUrl);
            logger.LogInformation("Activating chaos '{ScenarioId}' on {PodUrl}", scenarioId, podUrl);
            await client.PostAsync($"chaos/activate/{scenarioId}", null, ct);
        }
    }

    public async Task DeactivateAsync(string scenarioId, CancellationToken ct)
    {
        var podUrls = await podDiscovery.ResolvePodUrlsAsync(baseUrl, ct);
        foreach (var podUrl in podUrls)
        {
            var client = CreateClientForUrl(podUrl);
            logger.LogInformation("Deactivating chaos '{ScenarioId}' on {PodUrl}", scenarioId, podUrl);
            await client.DeleteAsync($"chaos/activate/{scenarioId}", ct);
        }
    }

    public async Task DeactivateAllAsync(CancellationToken ct)
    {
        var podUrls = await podDiscovery.ResolvePodUrlsAsync(baseUrl, ct);
        foreach (var podUrl in podUrls)
        {
            var client = CreateClientForUrl(podUrl);
            logger.LogInformation("Deactivating all chaos on {PodUrl}", podUrl);
            await client.DeleteAsync("chaos", ct);
        }
    }

    private HttpClient CreateClientForUrl(string podUrl)
    {
        var client = httpClientFactory.CreateClient(clientName);
        client.BaseAddress = new Uri(podUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(10);
        return client;
    }
}
