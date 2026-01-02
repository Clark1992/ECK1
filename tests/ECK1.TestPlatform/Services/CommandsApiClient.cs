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

public sealed record CommandsApiAccepted(Guid Id, List<Guid> EventIds);
