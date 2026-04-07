using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;

namespace ECK1.E2E.Tests.API;

/// <summary>
/// Shared configuration and HttpClient factory for E2E tests.
/// Reads gateway URL and Zitadel credentials from appsettings.json.
/// </summary>
public class GatewayFixture : IDisposable
{
    public HttpClient Client { get; }
    public E2ESettings Settings { get; }

    public GatewayFixture()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        Settings = config.Get<E2ESettings>()
            ?? throw new InvalidOperationException("Failed to bind E2ESettings from appsettings.json");

        if (string.IsNullOrEmpty(Settings.GatewayUrl))
            throw new InvalidOperationException("GatewayUrl is not configured in appsettings.json");

        Client = new HttpClient
        {
            BaseAddress = new Uri(Settings.GatewayUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };

        if (!string.IsNullOrEmpty(Settings.Auth.Url)
            && !string.IsNullOrEmpty(Settings.Auth.ClientId))
        {
            var token = ZitadelAuthHelper.ObtainUserToken(Settings);
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public void Dispose()
    {
        Client.Dispose();
    }
}

[CollectionDefinition("Gateway")]
public class GatewayCollection : ICollectionFixture<GatewayFixture>;
