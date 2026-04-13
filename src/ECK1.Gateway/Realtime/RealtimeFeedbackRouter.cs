using ECK1.Kafka;
using ECK1.RealtimeFeedback.Contracts;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace ECK1.Gateway.Realtime;

public class RealtimeFeedbackRouter(
    IHubContext<RealtimeHub> hubContext,
    IOptions<RealtimeConfig> options,
    ILogger<RealtimeFeedbackRouter> logger)
    : IKafkaMessageHandler<RealtimeFeedbackEvent>
{
    private readonly int _sendTimeoutMs = options.Value.SendTimeoutMs;

    public async Task Handle(string key, RealtimeFeedbackEvent message, KafkaMessageId messageId, CancellationToken ct)
    {
        logger.LogDebug(
            "Received feedback from Kafka: {EntityType}:{EntityId} (correlation: {CorrelationId})",
            message.EntityType, message.EntityId, message.CorrelationId);

        await RouteAsync(message, ct);
    }

    private async Task RouteAsync(RealtimeFeedbackEvent feedback, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(feedback.UserId))
        {
            logger.LogWarning("Received feedback without userId — dropping");
            return;
        }

        using var sendCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        sendCts.CancelAfter(_sendTimeoutMs);

        try
        {
            // Always route to entity group if entity info is present and its not a create flow
            if (!string.IsNullOrEmpty(feedback.EntityType) 
                && !string.IsNullOrEmpty(feedback.EntityId) 
                && feedback.Version > 1)
            {
                await hubContext.Clients
                    .Group($"entity:{feedback.EntityType}:{feedback.EntityId}")
                    .SendAsync("ReceiveFeedback", feedback, sendCts.Token);
            }

            if (!string.IsNullOrEmpty(feedback.CorrelationId))
            {
                await hubContext.Clients
                    .Group($"corr:{feedback.CorrelationId}")
                    .SendAsync("ReceiveFeedback", feedback, sendCts.Token);

                logger.LogDebug(
                    "Routed feedback to correlation group {CorrelationId} (user: {UserId})",
                    feedback.CorrelationId, feedback.UserId);
            }
            else
            {
                await hubContext.Clients
                    .Group($"user:{feedback.UserId}")
                    .SendAsync("ReceiveFeedback", feedback, sendCts.Token);

                logger.LogDebug(
                    "Routed feedback to user group {UserId}",
                    feedback.UserId);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning(
                "Send timeout for feedback {CorrelationId} (user: {UserId}) — dropped",
                feedback.CorrelationId, feedback.UserId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to route feedback {CorrelationId} (user: {UserId}) — dropped",
                feedback.CorrelationId, feedback.UserId);
        }
    }
}
