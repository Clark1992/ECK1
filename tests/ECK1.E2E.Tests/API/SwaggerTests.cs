using System.Net;
using System.Text.Json;

namespace ECK1.E2E.Tests.API;

[Collection("Gateway")]
public class SwaggerTests
{
    private readonly HttpClient _client;

    public SwaggerTests(GatewayFixture fixture)
    {
        _client = fixture.Client;
    }

    [Fact]
    public async Task SwaggerServices_ReturnsDiscoveredServices()
    {
        // Act
        var response = await _client.GetAsync("/swagger/services");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var services = await response.Content.ReadFromJsonAsync<JsonElement>();
        services.ValueKind.Should().Be(JsonValueKind.Array);
        services.GetArrayLength().Should().BeGreaterThanOrEqualTo(2);

        var names = services.EnumerateArray()
            .Select(s => s.GetProperty("name").GetString())
            .ToList();

        names.Should().Contain("eck1-commandsapi");
        names.Should().Contain("eck1-queriesapi");
    }

    [Fact]
    public async Task MergedSwagger_ReturnsValidOpenApiDocument()
    {
        // Act
        var response = await _client.GetAsync("/swagger/merged/swagger.json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
        doc.GetProperty("openapi").GetString().Should().Be("3.0.1");
        doc.GetProperty("info").GetProperty("title").GetString().Should().Contain("ECK1");
    }

    [Fact]
    public async Task MergedSwagger_ContainsSyncEndpoints()
    {
        // Act
        var doc = await _client.GetFromJsonAsync<JsonElement>("/swagger/merged/swagger.json");

        // Assert
        var paths = doc.GetProperty("paths");

        using var scope = new AssertionScope();
        paths.TryGetProperty("/eck1-queriesapi/api/samples", out _).Should().BeTrue("samples list endpoint should exist");
        paths.TryGetProperty("/eck1-queriesapi/api/samples/{id}", out _).Should().BeTrue("sample by id endpoint should exist");
        paths.TryGetProperty("/eck1-queriesapi/api/sample2s", out _).Should().BeTrue("sample2s list endpoint should exist");
        paths.TryGetProperty("/eck1-queriesapi/api/sample2s/{id}", out _).Should().BeTrue("sample2 by id endpoint should exist");
    }

    [Fact]
    public async Task MergedSwagger_ContainsAsyncCommandEndpoints()
    {
        // Act
        var doc = await _client.GetFromJsonAsync<JsonElement>("/swagger/merged/swagger.json");

        // Assert
        var paths = doc.GetProperty("paths");

        using var scope = new AssertionScope();
        paths.TryGetProperty("/eck1-commandsapi/api/async/sample", out _).Should().BeTrue("async sample create endpoint should exist");
        paths.TryGetProperty("/eck1-commandsapi/api/async/sample2", out _).Should().BeTrue("async sample2 create endpoint should exist");
    }

    [Fact]
    public async Task MergedSwagger_EndpointsAreTaggedByService()
    {
        // Act
        var doc = await _client.GetFromJsonAsync<JsonElement>("/swagger/merged/swagger.json");

        // Assert
        var paths = doc.GetProperty("paths");
        var samplesPath = paths.GetProperty("/eck1-queriesapi/api/samples");
        var getOp = samplesPath.GetProperty("get");
        var tags = getOp.GetProperty("tags");
        tags.GetArrayLength().Should().Be(1);
        tags[0].GetString().Should().Be("eck1-queriesapi");
    }

    [Fact]
    public async Task SwaggerUI_IsAccessible()
    {
        // Act
        var response = await _client.GetAsync("/swagger/index.html");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("swagger", "swagger UI page should contain swagger references");
    }

    [Fact]
    public async Task PerServiceSwagger_ReturnsDocForQueriesApi()
    {
        // Act
        var response = await _client.GetAsync("/swagger/eck1-queriesapi/swagger.json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
        doc.TryGetProperty("paths", out _).Should().BeTrue();
    }

    [Fact]
    public async Task PerServiceSwagger_ReturnsDocForCommandsApi()
    {
        // Act
        var response = await _client.GetAsync("/swagger/eck1-commandsapi/swagger.json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
        doc.TryGetProperty("paths", out _).Should().BeTrue();
    }
}
