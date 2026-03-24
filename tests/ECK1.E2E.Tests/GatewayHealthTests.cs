using System.Net;
using System.Text.Json;

namespace ECK1.E2E.Tests;

[Collection("Gateway")]
public class GatewayHealthTests
{
    private readonly HttpClient _client;

    public GatewayHealthTests(GatewayFixture fixture)
    {
        _client = fixture.Client;
    }

    [Fact]
    public async Task Health_ReturnsOkWithStatus()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("status").GetString().Should().Be("ok");
    }
}
