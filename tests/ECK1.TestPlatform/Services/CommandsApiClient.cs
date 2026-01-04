using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ECK1.TestPlatform.Services;

public sealed class CommandsApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    public CommandsApiClient(HttpClient http, IOptionsSnapshot<CommandsApiClientOptions> options)
    {
        var opt = options.Value;
        if (string.IsNullOrWhiteSpace(opt.BaseUrl))
            throw new InvalidOperationException($"Missing configuration: {CommandsApiClientOptions.SectionName}:BaseUrl");

        _http = http;
        _http.BaseAddress = new Uri(opt.BaseUrl, UriKind.Absolute);
        _http.Timeout = TimeSpan.FromSeconds(Math.Max(1, opt.TimeoutSeconds));
    }

    public async Task<CommandsApiAccepted?> CreateSampleAsync(CreateSampleRequest request, CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync("api/sample", request, JsonOptions, ct);
        return await ReadAcceptedAsync(response, ct);
    }

    public async Task<CommandsApiAccepted?> CreateSample2Async(CreateSample2Request request, CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync("api/sample2", request, JsonOptions, ct);
        return await ReadAcceptedAsync(response, ct);
    }

    public async Task<CommandsApiAccepted?> ChangeSample2CustomerEmailAsync(Guid id, string newEmail, CancellationToken ct)
    {
        // CommandsAPI expects a raw JSON string, not an object
        using var content = new StringContent(JsonSerializer.Serialize(newEmail, JsonOptions), Encoding.UTF8, "application/json");
        using var response = await _http.PutAsync($"api/sample2/{id}/customer-email", content, ct);
        return await ReadAcceptedAsync(response, ct);
    }

    public async Task<CommandsApiAccepted?> ChangeSample2StatusAsync(Guid id, int newStatus, string reason, CancellationToken ct)
    {
        var request = new ChangeSample2StatusRequest(newStatus, reason);
        using var response = await _http.PutAsJsonAsync($"api/sample2/{id}/status", request, JsonOptions, ct);
        return await ReadAcceptedAsync(response, ct);
    }

    public async Task<CommandsApiAccepted?> ChangeSample2ShippingAddressAsync(Guid id, Sample2AddressDto newAddress, CancellationToken ct)
    {
        using var response = await _http.PutAsJsonAsync($"api/sample2/{id}/shipping-address", newAddress, JsonOptions, ct);
        return await ReadAcceptedAsync(response, ct);
    }

    public async Task<CommandsApiAccepted?> ChangeSampleNameAsync(Guid id, string newName, CancellationToken ct)
    {
        // CommandsAPI expects a raw JSON string, not an object
        using var content = new StringContent(JsonSerializer.Serialize(newName, JsonOptions), Encoding.UTF8, "application/json");
        using var response = await _http.PutAsync($"api/sample/{id}/name", content, ct);
        return await ReadAcceptedAsync(response, ct);
    }

    public async Task<CommandsApiAccepted?> ChangeSampleDescriptionAsync(Guid id, string newDescription, CancellationToken ct)
    {
        using var content = new StringContent(JsonSerializer.Serialize(newDescription, JsonOptions), Encoding.UTF8, "application/json");
        using var response = await _http.PutAsync($"api/sample/{id}/description", content, ct);
        return await ReadAcceptedAsync(response, ct);
    }

    private static async Task<CommandsApiAccepted?> ReadAcceptedAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            return await response.Content.ReadFromJsonAsync<CommandsApiAccepted>(JsonOptions, ct);
        }

        // For load testing we mainly care about status; caller can count failures.
        return null;
    }
}

public sealed record CreateSampleRequest(string Name, string Description, SampleAddressDto? Address);

public sealed record SampleAddressDto(string Street, string City, string Country);

public sealed record CreateSample2Request(
    Sample2CustomerDto Customer,
    Sample2AddressDto ShippingAddress,
    List<Sample2LineItemDto> LineItems,
    List<string> Tags);

public sealed record Sample2CustomerDto(Guid CustomerId, string Email, string Segment);

public sealed record Sample2AddressDto(Guid Id, string Street, string City, string Country);

public sealed record Sample2MoneyDto(decimal Amount, string Currency);

public sealed record Sample2LineItemDto(Guid ItemId, string Sku, int Quantity, Sample2MoneyDto UnitPrice);

public sealed record ChangeSample2StatusRequest(int NewStatus, string Reason);

public sealed record CommandsApiAccepted(Guid Id, List<Guid> EventIds);
