using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ECK1.E2E.Tests;

/// <summary>
/// Creates a Sample2 via the sync command endpoint, waits for view projection,
/// and verifies the entity through query and search endpoints.
/// </summary>
[Collection("Gateway")]
public class Sample2EndToEndTests
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public Sample2EndToEndTests(GatewayFixture fixture)
    {
        _client = fixture.Client;
    }

    [Fact]
    public async Task CreateSample2_ThenQueryById_ReturnsAllFields()
    {
        // Arrange
        var email = $"e2e-{Guid.NewGuid().ToString("N")[..8]}@test.com";
        var body = new
        {
            customer = new { email, segment = "VIP" },
            shippingAddress = new { street = "456 Order St", city = "OrderCity", country = "DE" },
            lineItems = new[]
            {
                new { sku = "SKU-E2E-001", quantity = 3, unitPrice = new { amount = 25.50m, currency = "EUR" } }
            },
            tags = new[] { "e2e-test", "automated" }
        };

        // Act — create via sync endpoint
        var createResponse = await _client.PostAsJsonAsync("/eck1-commandsapi/api/sync/sample2", body);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var createResult = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var sample2Id = createResult.GetProperty("id").GetGuid();
        sample2Id.Should().NotBeEmpty();

        // Wait for view projection
        await Task.Delay(3000);

        // Act — query by ID
        var getResponse = await _client.GetAsync($"/eck1-queriesapi/api/sample2s/{sample2Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var sample2 = await getResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        // Assert
        using var scope = new AssertionScope();
        sample2.GetProperty("sample2Id").GetGuid().Should().Be(sample2Id);

        var customer = sample2.GetProperty("customer");
        customer.GetProperty("email").GetString().Should().Be(email);
        customer.GetProperty("segment").GetString().Should().Be("VIP");

        var address = sample2.GetProperty("shippingAddress");
        address.GetProperty("street").GetString().Should().Be("456 Order St");
        address.GetProperty("city").GetString().Should().Be("OrderCity");
        address.GetProperty("country").GetString().Should().Be("DE");

        var lineItems = sample2.GetProperty("lineItems");
        lineItems.GetArrayLength().Should().Be(1);
        var item = lineItems[0];
        item.GetProperty("sku").GetString().Should().Be("SKU-E2E-001");
        item.GetProperty("quantity").GetInt32().Should().Be(3);
        item.GetProperty("unitPrice").GetProperty("amount").GetDecimal().Should().Be(25.50m);
        item.GetProperty("unitPrice").GetProperty("currency").GetString().Should().Be("EUR");

        var tags = sample2.GetProperty("tags");
        tags.GetArrayLength().Should().Be(2);

        sample2.GetProperty("status").GetInt32().Should().Be(0, "new sample2 should have Draft (0) status");
    }

    [Fact]
    public async Task CreateSample2_ThenChangeCustomerEmail_ReflectsInQuery()
    {
        // Arrange — create
        var body = new
        {
            customer = new { email = "original@test.com", segment = "Standard" },
            shippingAddress = new { street = "St", city = "City", country = "US" },
            lineItems = new[]
            {
                new { sku = "SKU-1", quantity = 1, unitPrice = new { amount = 10.0m, currency = "USD" } }
            },
            tags = new[] { "test" }
        };
        var createResponse = await _client.PostAsJsonAsync("/eck1-commandsapi/api/sync/sample2", body);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var sample2Id = (await createResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await Task.Delay(2000);

        // Act — change email
        var updateResponse = await _client.PutAsJsonAsync(
            $"/eck1-commandsapi/api/sync/sample2/{sample2Id}/customer-email",
            "updated@test.com");
        updateResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        await Task.Delay(2000);

        // Assert
        var sample2 = await _client.GetFromJsonAsync<JsonElement>(
            $"/eck1-queriesapi/api/sample2s/{sample2Id}", JsonOptions);
        sample2.GetProperty("customer").GetProperty("email").GetString().Should().Be("updated@test.com");
    }

    [Fact]
    public async Task GetSample2s_ReturnsPaginatedList()
    {
        // Act
        var response = await _client.GetAsync("/eck1-queriesapi/api/sample2s?top=5&skip=0");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        // Assert
        using var scope = new AssertionScope();
        result.TryGetProperty("items", out var items).Should().BeTrue("response should have items array");
        result.TryGetProperty("total", out var total).Should().BeTrue("response should have total count");
        items.ValueKind.Should().Be(JsonValueKind.Array);
        total.GetInt64().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetSample2ById_NonExistent_Returns404()
    {
        // Arrange
        var fakeId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/eck1-queriesapi/api/sample2s/{fakeId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SearchSample2s_ReturnsPagedResponse()
    {
        // Act
        var response = await _client.GetAsync("/eck1-queriesapi/search/sample2s?top=5&skip=0");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        // Assert
        using var scope = new AssertionScope();
        result.TryGetProperty("items", out var items).Should().BeTrue("search response should have items array");
        result.TryGetProperty("total", out var total).Should().BeTrue("search response should have total count");
        items.ValueKind.Should().Be(JsonValueKind.Array);
        total.GetInt64().Should().BeGreaterThanOrEqualTo(0);
    }
}
