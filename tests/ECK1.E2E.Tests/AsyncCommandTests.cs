using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ECK1.E2E.Tests;

/// <summary>
/// Tests the gateway's HTTP → Kafka async command pipeline.
/// Verifies that async endpoints return 202 Accepted with the expected response shape.
/// </summary>
[Collection("Gateway")]
public class AsyncCommandTests
{
    private readonly HttpClient _client;

    public AsyncCommandTests(GatewayFixture fixture)
    {
        _client = fixture.Client;
    }

    [Fact]
    public async Task AsyncCreateSample_Returns202WithExpectedShape()
    {
        // Arrange
        var body = new
        {
            name = $"AsyncE2E-{Guid.NewGuid().ToString("N")[..8]}",
            description = "Async E2E test",
            address = new { street = "Async St", city = "AsyncCity", country = "US" }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/eck1-commandsapi/api/async/sample", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        using var scope = new AssertionScope();
        result.GetProperty("status").GetString().Should().Be("accepted");
        result.GetProperty("command").GetString().Should().Be("CreateSampleCommand");
        result.GetProperty("topic").GetString().Should().Be("sample-commands");
        result.TryGetProperty("key", out _).Should().BeTrue("response should contain a message key");
    }

    [Fact]
    public async Task AsyncCreateSample2_Returns202WithExpectedShape()
    {
        // Arrange
        var body = new
        {
            customer = new { email = "async-e2e@test.com", segment = "VIP" },
            shippingAddress = new { street = "Async St", city = "AsyncCity", country = "DE" },
            lineItems = new[]
            {
                new { sku = "ASYNC-SKU", quantity = 1, unitPrice = new { amount = 5.0m, currency = "USD" } }
            },
            tags = new[] { "async-test" }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/eck1-commandsapi/api/async/sample2", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        using var scope = new AssertionScope();
        result.GetProperty("status").GetString().Should().Be("accepted");
        result.GetProperty("command").GetString().Should().Be("CreateSample2Command");
        result.GetProperty("topic").GetString().Should().Be("sample2-commands");
        result.TryGetProperty("key", out _).Should().BeTrue("response should contain a message key");
    }

    [Fact]
    public async Task AsyncUpdateSampleName_Returns202()
    {
        // Arrange — create a sample first via sync to get an ID
        var createBody = new
        {
            name = "ForAsyncUpdate",
            description = "desc",
            address = new { street = "St", city = "City", country = "US" }
        };
        var createResponse = await _client.PostAsJsonAsync("/eck1-commandsapi/api/sync/sample", createBody);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var sampleId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // Act — update name via async endpoint
        var updateBody = new { newName = "AsyncUpdatedName" };
        var response = await _client.PutAsJsonAsync(
            $"/eck1-commandsapi/api/async/sample/{sampleId}/name", updateBody);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("command").GetString().Should().Be("ChangeSampleNameCommand");
    }

    [Fact]
    public async Task AsyncCommand_WithInvalidJson_Returns400()
    {
        // Arrange
        var content = new StringContent("{invalid-json}", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/eck1-commandsapi/api/async/sample", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
