using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ECK1.TestPlatform.Services;

public sealed class CommandsApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int MaxConflictRetries = 3;
    private const int MaxAllowedTimeoutSeconds = 30;

    private readonly HttpClient _http;

    public CommandsApiClient(HttpClient http, IOptionsSnapshot<CommandsApiClientOptions> options)
    {
        var opt = options.Value;
        if (string.IsNullOrWhiteSpace(opt.BaseUrl))
            throw new InvalidOperationException($"Missing configuration: {CommandsApiClientOptions.SectionName}:BaseUrl");

        _http = http;
        _http.BaseAddress = new Uri(opt.BaseUrl, UriKind.Absolute);
        _http.Timeout = TimeSpan.FromSeconds(Math.Clamp(opt.TimeoutSeconds, 1, MaxAllowedTimeoutSeconds));
    }

    public async Task<CommandsApiAccepted?> CreateSampleAsync(CreateSampleRequest request, CancellationToken ct)
    {
        return await SendWithConflictRetryAsync(
            token => _http.PostAsJsonAsync("api/sample", request, JsonOptions, token),
            ct);
    }

    public async Task<CommandsApiAccepted?> CreateSample2Async(CreateSample2Request request, CancellationToken ct)
    {
        return await SendWithConflictRetryAsync(
            token => _http.PostAsJsonAsync("api/sample2", request, JsonOptions, token),
            ct);
    }

    public async Task<CommandsApiAccepted?> ChangeSample2CustomerEmailAsync(Guid id, string newEmail, CancellationToken ct)
    {
        return await SendWithConflictRetryAsync(async token =>
        {
            using var content = new StringContent(JsonSerializer.Serialize(newEmail, JsonOptions), Encoding.UTF8, "application/json");
            return await _http.PutAsync($"api/sample2/{id}/customer-email", content, token);
        }, ct);
    }

    public async Task<CommandsApiAccepted?> ChangeSample2StatusAsync(Guid id, int newStatus, string reason, CancellationToken ct)
    {
        var request = new ChangeSample2StatusRequest(newStatus, reason);
        return await SendWithConflictRetryAsync(
            token => _http.PutAsJsonAsync($"api/sample2/{id}/status", request, JsonOptions, token),
            ct);
    }

    public async Task<CommandsApiAccepted?> ChangeSample2ShippingAddressAsync(Guid id, Sample2AddressDto newAddress, CancellationToken ct)
    {
        return await SendWithConflictRetryAsync(
            token => _http.PutAsJsonAsync($"api/sample2/{id}/shipping-address", newAddress, JsonOptions, token),
            ct);
    }

    public async Task<CommandsApiAccepted?> ChangeSampleNameAsync(Guid id, string newName, CancellationToken ct)
    {
        return await SendWithConflictRetryAsync(async token =>
        {
            using var content = new StringContent(JsonSerializer.Serialize(newName, JsonOptions), Encoding.UTF8, "application/json");
            return await _http.PutAsync($"api/sample/{id}/name", content, token);
        }, ct);
    }

    public async Task<CommandsApiAccepted?> ChangeSampleDescriptionAsync(Guid id, string newDescription, CancellationToken ct)
    {
        return await SendWithConflictRetryAsync(async token =>
        {
            using var content = new StringContent(JsonSerializer.Serialize(newDescription, JsonOptions), Encoding.UTF8, "application/json");
            return await _http.PutAsync($"api/sample/{id}/description", content, token);
        }, ct);
    }

    private async Task<CommandsApiAccepted?> SendWithConflictRetryAsync(
        Func<CancellationToken, Task<HttpResponseMessage>> send,
        CancellationToken ct)
    {
        for (var attempt = 0; attempt <= MaxConflictRetries; attempt++)
        {
            using var response = await send(ct);

            if (response.StatusCode == HttpStatusCode.Accepted)
            {
                return await response.Content.ReadFromJsonAsync<CommandsApiAccepted>(JsonOptions, ct);
            }

            if (response.StatusCode != HttpStatusCode.Conflict || attempt == MaxConflictRetries)
            {
                return null;
            }

            var delayMs = ComputeRetryDelayMs(attempt);
            await Task.Delay(delayMs, ct);
        }

        return null;
    }

    private static int ComputeRetryDelayMs(int attempt)
    {
        var baseDelay = 25 * (1 << Math.Min(attempt, 5));
        var jitter = Random.Shared.Next(0, 30);
        return baseDelay + jitter;
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
