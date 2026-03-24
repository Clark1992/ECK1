namespace ECK1.E2E.Tests;

/// <summary>
/// Shared configuration and HttpClient factory for E2E tests.
/// Reads the gateway URL from the GATEWAY_URL environment variable
/// or defaults to http://localhost:30090.
/// </summary>
public class GatewayFixture : IDisposable
{
    public HttpClient Client { get; }
    public string GatewayUrl { get; }

    public GatewayFixture()
    {
        GatewayUrl = Environment.GetEnvironmentVariable("GATEWAY_URL") ?? "http://localhost:30090";
        Client = new HttpClient
        {
            BaseAddress = new Uri(GatewayUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public void Dispose()
    {
        Client.Dispose();
    }
}

[CollectionDefinition("Gateway")]
public class GatewayCollection : ICollectionFixture<GatewayFixture>;
