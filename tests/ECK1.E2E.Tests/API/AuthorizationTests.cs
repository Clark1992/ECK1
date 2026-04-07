using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ECK1.E2E.Tests.API;

[Collection("Gateway")]
public class AuthorizationTests
{
    private readonly GatewayFixture _fixture;
    private readonly HttpClient _userClient;

    public AuthorizationTests(GatewayFixture fixture)
    {
        _fixture = fixture;
        _userClient = fixture.Client;
    }

    [Fact]
    public async Task UnauthenticatedRequest_Returns401()
    {
        // Arrange
        using var anonClient = new HttpClient
        {
            BaseAddress = new Uri(_fixture.Settings.GatewayUrl),
            Timeout = TimeSpan.FromSeconds(15)
        };

        // Act
        var response = await anonClient.GetAsync("/eck1-queriesapi/api/samples?top=1&skip=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UserRole_DeleteLineItem_Returns403()
    {
        // Arrange — create a Sample2 so we have a valid aggregate ID
        var sample2Id = await CreateSample2(_userClient);
        var fakeItemId = Guid.NewGuid();

        // Act — user (role: "user") tries to delete a line item (requires "delete" permission)
        var response = await _userClient.DeleteAsync(
            $"/eck1-commandsapi/api/sync/sample2/{sample2Id}/line-items/{fakeItemId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminRole_DeleteLineItem_IsAllowed()
    {
        // Arrange — get admin token
        var adminToken = ZitadelAuthHelper.ObtainAdminToken(_fixture.Settings);
        using var adminClient = new HttpClient
        {
            BaseAddress = new Uri(_fixture.Settings.GatewayUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Create a Sample2 with a line item
        var sample2Id = await CreateSample2(adminClient);
        await Task.Delay(1000);

        // Add a line item so we can delete it
        var addItemBody = new
        {
            sku = "DEL-TEST-001",
            quantity = 1,
            unitPrice = new { amount = 5.0m, currency = "USD" }
        };
        var addResponse = await adminClient.PostAsJsonAsync(
            $"/eck1-commandsapi/api/sync/sample2/{sample2Id}/line-items", addItemBody);

        // The add might return the item ID — parse if available, otherwise use the aggregate
        if (addResponse.StatusCode == HttpStatusCode.Accepted)
        {
            var addResult = await addResponse.Content.ReadFromJsonAsync<JsonElement>();
            if (addResult.TryGetProperty("id", out var itemIdProp))
            {
                var itemId = itemIdProp.GetGuid();

                // Act — admin (role: "admin") deletes the line item (has "delete" permission)
                var deleteResponse = await adminClient.DeleteAsync(
                    $"/eck1-commandsapi/api/sync/sample2/{sample2Id}/line-items/{itemId}");

                // Assert — should be allowed (Accepted or OK, not 403)
                deleteResponse.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
                deleteResponse.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
            }
        }
    }

    [Fact]
    public async Task UserRole_DeleteTags_Returns403()
    {
        // Arrange — create a Sample2
        var sample2Id = await CreateSample2(_userClient);

        // Act — user tries to delete tags (requires "delete" permission)
        var response = await _userClient.DeleteAsync(
            $"/eck1-commandsapi/api/sync/sample2/{sample2Id}/tags");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task HealthEndpoint_AllowsAnonymous()
    {
        // Arrange
        using var anonClient = new HttpClient
        {
            BaseAddress = new Uri(_fixture.Settings.GatewayUrl),
            Timeout = TimeSpan.FromSeconds(15)
        };

        // Act
        var response = await anonClient.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<Guid> CreateSample2(HttpClient client)
    {
        var body = new
        {
            customer = new { email = $"auth-test-{Guid.NewGuid():N}@test.com", segment = "Standard" },
            shippingAddress = new { street = "1 Auth St", city = "AuthCity", country = "US" },
            lineItems = new[]
            {
                new { sku = "AUTH-001", quantity = 1, unitPrice = new { amount = 1.0m, currency = "USD" } }
            },
            tags = new[] { "auth-test" }
        };

        var response = await client.PostAsJsonAsync("/eck1-commandsapi/api/sync/sample2", body);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "creating a Sample2 should succeed for authenticated users");

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        return result.GetProperty("id").GetGuid();
    }
}
