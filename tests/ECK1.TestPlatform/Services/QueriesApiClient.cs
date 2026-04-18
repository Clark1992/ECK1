using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ECK1.TestPlatform.Services;

public sealed class QueriesApiClientOptions
{
    public const string SectionName = "QueriesApi";
    public string BaseUrl { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 30;
}

public sealed class QueriesApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    public QueriesApiClient(HttpClient http, IOptions<QueriesApiClientOptions> options)
    {
        var opt = options.Value;
        if (string.IsNullOrWhiteSpace(opt.BaseUrl))
            throw new InvalidOperationException($"Missing configuration: {QueriesApiClientOptions.SectionName}:BaseUrl");

        _http = http;
        _http.BaseAddress = new Uri(opt.BaseUrl, UriKind.Absolute);
        _http.Timeout = TimeSpan.FromSeconds(Math.Clamp(opt.TimeoutSeconds, 1, 60));
    }

    public async Task<JsonElement?> GetSampleByIdAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var response = await _http.GetAsync($"api/samples/{id}", ct);
            if (!response.IsSuccessStatusCode)
                return null;
            return await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct);
        }
        catch
        {
            return null;
        }
    }

    public async Task<JsonElement?> GetSample2ByIdAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var response = await _http.GetAsync($"api/sample2s/{id}", ct);
            if (!response.IsSuccessStatusCode)
                return null;
            return await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct);
        }
        catch
        {
            return null;
        }
    }

    public async Task<JsonElement?> SearchSampleByIdAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var response = await _http.GetAsync($"search/samples?q={id}", ct);
            if (!response.IsSuccessStatusCode)
                return null;
            return await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct);
        }
        catch
        {
            return null;
        }
    }

    public async Task<JsonElement?> SearchSample2ByIdAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var response = await _http.GetAsync($"search/sample2s?q={id}", ct);
            if (!response.IsSuccessStatusCode)
                return null;
            return await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct);
        }
        catch
        {
            return null;
        }
    }

    public async Task<JsonElement?> GetSampleHistoryAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var response = await _http.GetAsync($"api/history/samples/{id}", ct);
            if (!response.IsSuccessStatusCode)
                return null;
            var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct);
            // The response is { events: [...] } — unwrap to the array
            if (result.TryGetProperty("events", out var events))
                return events;
            return result;
        }
        catch
        {
            return null;
        }
    }
}
