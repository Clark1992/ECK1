using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ECK1.E2E.Tests;

/// <summary>
/// Creates a Sample via the sync command endpoint, waits for view projection,
/// and verifies the entity through query and search endpoints.
/// </summary>
[Collection("Gateway")]
public class SampleEndToEndTests
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public SampleEndToEndTests(GatewayFixture fixture)
    {
        _client = fixture.Client;
    }

    [Fact]
    public async Task CreateSample_ThenQueryById_ReturnsAllFields()
    {
        // Arrange
        var name = $"E2E-Sample-{Guid.NewGuid().ToString("N")[..8]}";
        var body = new
        {
            name,
            description = "E2E test description",
            address = new { street = "123 Test St", city = "TestCity", country = "US" }
        };

        // Act — create via sync endpoint
        var createResponse = await _client.PostAsJsonAsync("/eck1-commandsapi/api/sync/sample", body);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var createResult = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var sampleId = createResult.GetProperty("id").GetGuid();
        sampleId.Should().NotBeEmpty();

        // Wait for view projection
        await Task.Delay(3000);

        // Act — query by ID
        var getResponse = await _client.GetAsync($"/eck1-queriesapi/api/samples/{sampleId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var sample = await getResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        // Assert
        using var scope = new AssertionScope();
        sample.GetProperty("sampleId").GetGuid().Should().Be(sampleId);
        sample.GetProperty("name").GetString().Should().Be(name);
        sample.GetProperty("description").GetString().Should().Be("E2E test description");

        var address = sample.GetProperty("address");
        address.GetProperty("street").GetString().Should().Be("123 Test St");
        address.GetProperty("city").GetString().Should().Be("TestCity");
        address.GetProperty("country").GetString().Should().Be("US");

        sample.TryGetProperty("attachments", out var attachments).Should().BeTrue();
        attachments.ValueKind.Should().BeOneOf(JsonValueKind.Array, JsonValueKind.Null);
    }

    [Fact]
    public async Task CreateSample_ThenUpdateName_ReflectsInQuery()
    {
        // Arrange — create
        var body = new
        {
            name = "OriginalName",
            description = "desc",
            address = new { street = "St", city = "City", country = "US" }
        };
        var createResponse = await _client.PostAsJsonAsync("/eck1-commandsapi/api/sync/sample", body);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var sampleId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await Task.Delay(2000);

        // Act — update name
        var updateResponse = await _client.PutAsJsonAsync($"/eck1-commandsapi/api/sync/sample/{sampleId}/name", "UpdatedName");
        updateResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        await Task.Delay(2000);

        // Assert
        var sample = await _client.GetFromJsonAsync<JsonElement>($"/eck1-queriesapi/api/samples/{sampleId}", JsonOptions);
        sample.GetProperty("name").GetString().Should().Be("UpdatedName");
    }

    [Fact]
    public async Task GetSamples_ReturnsPaginatedList()
    {
        // Act
        var response = await _client.GetAsync("/eck1-queriesapi/api/samples?top=5&skip=0");
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
    public async Task GetSampleById_NonExistent_Returns404()
    {
        // Arrange
        var fakeId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/eck1-queriesapi/api/samples/{fakeId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SearchSamples_ReturnsPagedResponse()
    {
        // Act
        var response = await _client.GetAsync("/eck1-queriesapi/search/samples?top=5&skip=0");
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
