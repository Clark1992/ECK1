using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;

namespace ECK1.TestPlatform.Services;

public sealed class GatewayRealtimeClientOptions
{
    public const string SectionName = "GatewayRealtime";
    public string HubUrl { get; set; } = "";
    public int DefaultTimeoutSeconds { get; set; } = 30;
}

public sealed class RealtimeFeedback
{
    public string CorrelationId { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string EntityId { get; set; } = "";
    public bool Success { get; set; }
    public string OutcomeCode { get; set; } = "";
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public int Version { get; set; }
}

/// <summary>
/// Connects to the Gateway's SignalR RealtimeHub and provides correlation-based
/// feedback subscriptions, mimicking how the FE receives command results.
/// </summary>
public sealed class GatewayRealtimeClient : IAsyncDisposable
{
    private readonly BearerTokenStore _tokenStore;
    private readonly GatewayRealtimeClientOptions _options;
    private readonly ILogger<GatewayRealtimeClient> _logger;
    private HubConnection? _connection;
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private string? _connectedToken;

    public GatewayRealtimeClient(
        BearerTokenStore tokenStore,
        Microsoft.Extensions.Options.IOptions<GatewayRealtimeClientOptions> options,
        ILogger<GatewayRealtimeClient> logger)
    {
        _tokenStore = tokenStore;
        _options = options.Value;
        _logger = logger;
    }

    private async Task EnsureConnectedAsync(CancellationToken ct)
    {
        var currentToken = _tokenStore.Token;
        if (_connection?.State == HubConnectionState.Connected && _connectedToken == currentToken)
            return;

        await _connectLock.WaitAsync(ct);
        try
        {
            currentToken = _tokenStore.Token;
            if (_connection?.State == HubConnectionState.Connected && _connectedToken == currentToken)
                return;

            if (string.IsNullOrWhiteSpace(currentToken))
                throw new InvalidOperationException("Cannot connect to Gateway RealtimeHub without a forwarded bearer token.");

            if (_connection is not null)
            {
                await _connection.DisposeAsync();
                _connection = null;
            }

            var hubUrl = _options.HubUrl;
            if (string.IsNullOrWhiteSpace(hubUrl))
                throw new InvalidOperationException($"Missing configuration: {GatewayRealtimeClientOptions.SectionName}:HubUrl");

            _connection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult(_tokenStore.Token);
                })
                .WithAutomaticReconnect([TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5)])
                .Build();

            await _connection.StartAsync(ct);
            _connectedToken = currentToken;
            _logger.LogInformation("Connected to Gateway RealtimeHub at {Url}", hubUrl);
        }
        finally
        {
            _connectLock.Release();
        }
    }

    /// <summary>
    /// Subscribes to a correlation, sends a command via the provided delegate, then waits
    /// for a feedback event matching the predicate. Returns the feedback event.
    /// This mimics the FE flow: subscribe → send → wait for ReceiveFeedback.
    /// </summary>
    public async Task<RealtimeFeedback?> SendAndWaitForFeedbackAsync(
        string correlationId,
        Func<Task> sendCommand,
        Func<RealtimeFeedback, bool>? feedbackFilter = null,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        await EnsureConnectedAsync(ct);
        var connection = _connection ?? throw new InvalidOperationException("Gateway RealtimeHub connection is not available.");

        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(_options.DefaultTimeoutSeconds);
        var tcs = new TaskCompletionSource<RealtimeFeedback>(TaskCreationOptions.RunContinuationsAsynchronously);

        IDisposable? subscription = null;
        subscription = connection.On<JsonElement>("ReceiveFeedback", feedbackJson =>
        {
            try
            {
                var feedback = JsonSerializer.Deserialize<RealtimeFeedback>(feedbackJson.GetRawText(),
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));

                if (feedback is null || feedback.CorrelationId != correlationId)
                    return;

                if (!feedback.Success)
                {
                    tcs.TrySetResult(feedback);
                    return;
                }

                if (feedbackFilter is not null && !feedbackFilter(feedback))
                    return;

                tcs.TrySetResult(feedback);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize feedback for correlation {CorrelationId}", correlationId);
            }
        });

        try
        {
            await connection.InvokeAsync("SubscribeToCorrelation", correlationId, ct);

            await sendCommand();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(effectiveTimeout);
            cts.Token.Register(() => tcs.TrySetCanceled(cts.Token));

            return await tcs.Task;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Feedback timeout for correlation {CorrelationId} after {Timeout}s",
                correlationId, effectiveTimeout.TotalSeconds);
            return null;
        }
        finally
        {
            subscription?.Dispose();
            try
            {
                await connection.InvokeAsync("UnsubscribeFromCorrelation", correlationId, CancellationToken.None);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }

        _connectedToken = null;

        _connectLock.Dispose();
    }
}
