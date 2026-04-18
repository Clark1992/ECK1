namespace ECK1.TestPlatform.Services;

public sealed class DirectChaosClient(
    IHttpClientFactory httpClientFactory,
    string clientName,
    string baseUrl,
    ILogger logger) : IChaosClient
{
    public async Task ActivateAsync(string scenarioId, CancellationToken ct)
    {
        var client = CreateClient();
        logger.LogInformation("Activating chaos '{ScenarioId}' on {BaseUrl}", scenarioId, baseUrl);
        await client.PostAsync($"chaos/activate/{scenarioId}", null, ct);
    }

    public async Task DeactivateAsync(string scenarioId, CancellationToken ct)
    {
        var client = CreateClient();
        logger.LogInformation("Deactivating chaos '{ScenarioId}' on {BaseUrl}", scenarioId, baseUrl);
        await client.DeleteAsync($"chaos/activate/{scenarioId}", ct);
    }

    public async Task DeactivateAllAsync(CancellationToken ct)
    {
        var client = CreateClient();
        logger.LogInformation("Deactivating all chaos on {BaseUrl}", baseUrl);
        await client.DeleteAsync("chaos", ct);
    }

    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient(clientName);
        client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(10);
        return client;
    }
}
