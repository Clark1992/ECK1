using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ECK1.TestPlatform.Services;

public sealed class CommandsApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int MaxAllowedTimeoutSeconds = 300;
    private const string CorrelationHeaderName = "X-Realtime-Correlation-Id";

    private readonly HttpClient _http;
    private readonly GatewayRealtimeClient _realtimeClient;
    private readonly ILogger<CommandsApiClient> _logger;

    public CommandsApiClient(
        HttpClient http,
        IOptions<CommandsApiClientOptions> options,
        GatewayRealtimeClient realtimeClient,
        ILogger<CommandsApiClient> logger)
    {
        var opt = options.Value;
        if (string.IsNullOrWhiteSpace(opt.BaseUrl))
            throw new InvalidOperationException($"Missing configuration: {CommandsApiClientOptions.SectionName}:BaseUrl");

        _http = http;
        _realtimeClient = realtimeClient;
        _logger = logger;
        _http.BaseAddress = new Uri(opt.BaseUrl, UriKind.Absolute);
        _http.Timeout = TimeSpan.FromSeconds(Math.Clamp(opt.TimeoutSeconds, 1, MaxAllowedTimeoutSeconds));
    }

    public async Task<CommandsApiAccepted?> CreateSampleAsync(CreateSampleRequest request, CancellationToken ct)
    {
        return await SendWithRealtimeFeedbackAsync(
            HttpMethod.Post,
            "api/async/sample",
            request,
            ct);
    }

    public async Task<CommandsApiAccepted?> CreateSample2Async(CreateSample2Request request, CancellationToken ct)
    {
        return await SendWithRealtimeFeedbackAsync(
            HttpMethod.Post,
            "api/async/sample2",
            request,
            ct);
    }

    public async Task<CommandsApiAccepted?> ChangeSample2CustomerEmailAsync(Guid id, string newEmail, CancellationToken ct, int expectedVersion = 0)
    {
        return await SendWithRealtimeFeedbackAsync(
            HttpMethod.Put,
            $"api/async/sample2/{id}/customer-email",
            new { NewEmail = newEmail, ExpectedVersion = expectedVersion },
            ct);
    }

    public async Task<CommandsApiAccepted?> ChangeSample2StatusAsync(Guid id, int newStatus, string reason, CancellationToken ct, int expectedVersion = 0)
    {
        var request = new ChangeSample2StatusRequest(newStatus, reason, expectedVersion);
        return await SendWithRealtimeFeedbackAsync(
            HttpMethod.Put,
            $"api/async/sample2/{id}/status",
            request,
            ct);
    }

    public async Task<CommandsApiAccepted?> ChangeSample2ShippingAddressAsync(Guid id, Sample2AddressDto newAddress, CancellationToken ct, int expectedVersion = 0)
    {
        return await SendWithRealtimeFeedbackAsync(
            HttpMethod.Put,
            $"api/async/sample2/{id}/shipping-address",
            new { NewAddress = newAddress, ExpectedVersion = expectedVersion },
            ct);
    }

    public async Task<CommandsApiAccepted?> ChangeSampleNameAsync(Guid id, string newName, CancellationToken ct, int expectedVersion = 0)
    {
        return await SendWithRealtimeFeedbackAsync(
            HttpMethod.Put,
            $"api/async/sample/{id}/name",
            new { NewName = newName, ExpectedVersion = expectedVersion },
            ct);
    }

    public async Task<CommandsApiAccepted?> ChangeSampleDescriptionAsync(Guid id, string newDescription, CancellationToken ct, int expectedVersion = 0)
    {
        return await SendWithRealtimeFeedbackAsync(
            HttpMethod.Put,
            $"api/async/sample/{id}/description",
            new { NewDescription = newDescription, ExpectedVersion = expectedVersion },
            ct);
    }

    private async Task<CommandsApiAccepted?> SendWithRealtimeFeedbackAsync(
        HttpMethod method,
        string requestUri,
        object? body,
        CancellationToken ct)
    {
        var correlationId = Guid.NewGuid().ToString("D");

        try
        {
            var feedback = await _realtimeClient.SendAndWaitForFeedbackAsync(
                correlationId,
                () => SendAcceptedCommandAsync(method, requestUri, body, correlationId, ct),
                feedback => feedback.Success && !string.IsNullOrWhiteSpace(feedback.EntityId),
                _http.Timeout,
                ct);

            if (feedback is null)
            {
                _logger.LogWarning("No realtime feedback received for {Method} {RequestUri} (correlation {CorrelationId})",
                    method, requestUri, correlationId);
                return null;
            }

            if (!feedback.Success)
            {
                _logger.LogWarning(
                    "Realtime command failure for {Method} {RequestUri} (correlation {CorrelationId}): {OutcomeCode} {Message}",
                    method,
                    requestUri,
                    correlationId,
                    feedback.OutcomeCode,
                    feedback.Message);
                return null;
            }

            if (!Guid.TryParse(feedback.EntityId, out var entityId))
            {
                _logger.LogWarning(
                    "Realtime feedback for {Method} {RequestUri} contained invalid entity id '{EntityId}' (correlation {CorrelationId})",
                    method,
                    requestUri,
                    feedback.EntityId,
                    correlationId);
                return null;
            }

            return new CommandsApiAccepted(entityId, feedback.Version, feedback.CorrelationId, feedback.OutcomeCode);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to execute correlated async command {Method} {RequestUri} (correlation {CorrelationId})",
                method,
                requestUri,
                correlationId);
            return null;
        }
    }

    private async Task SendAcceptedCommandAsync(
        HttpMethod method,
        string requestUri,
        object? body,
        string correlationId,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Add(CorrelationHeaderName, correlationId);

        if (body is not null)
            request.Content = JsonContent.Create(body, options: JsonOptions);

        using var response = await _http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.Accepted)
            return;

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        throw new HttpRequestException(
            $"Gateway command request failed with status {(int)response.StatusCode} ({response.ReasonPhrase}). Body: {responseBody}");
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

public sealed record ChangeSample2StatusRequest(int NewStatus, string Reason, int ExpectedVersion);

public sealed record CommandsApiAccepted(Guid Id, int Version, string CorrelationId, string OutcomeCode);
