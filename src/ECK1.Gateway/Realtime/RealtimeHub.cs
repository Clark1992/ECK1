#nullable enable
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ECK1.Gateway.Realtime;

[Authorize]
public class RealtimeHub(
    IRealtimeConnectionManager connectionManager,
    ILogger<RealtimeHub> logger) : Hub
{
    public override Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            logger.LogWarning("Authenticated connection without sub claim — aborting");
            Context.Abort();
            return Task.CompletedTask;
        }

        connectionManager.AddConnection(Context.ConnectionId, userId);

        // Add this connection to the user's group for broadcast fallback
        return Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = connectionManager.GetUserIdByConnection(Context.ConnectionId);
        connectionManager.RemoveConnection(Context.ConnectionId);

        if (userId is not null)
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{userId}");

        if (exception is not null)
            logger.LogWarning(exception, "Connection {ConnectionId} disconnected with error", Context.ConnectionId);
    }

    /// <summary>
    /// Subscribe the current connection to receive notifications for a specific correlationId.
    /// Only the browser tab that initiated the async request should call this.
    /// </summary>
    public async Task SubscribeToCorrelation(string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
            return;

        var userId = Context.User?.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return;

        await Groups.AddToGroupAsync(Context.ConnectionId, $"corr:{correlationId}");

        logger.LogDebug("Connection {ConnectionId} subscribed to correlation {CorrelationId}",
            Context.ConnectionId, correlationId);
    }

    /// <summary>
    /// Unsubscribe from correlation-based notifications (e.g., after timeout or result received).
    /// </summary>
    public async Task UnsubscribeFromCorrelation(string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
            return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"corr:{correlationId}");

        logger.LogDebug("Connection {ConnectionId} unsubscribed from correlation {CorrelationId}",
            Context.ConnectionId, correlationId);
    }

    /// <summary>
    /// Subscribe the current connection to receive notifications for a specific entity.
    /// Any tab that displays the entity should call this.
    /// </summary>
    public async Task SubscribeToEntity(string entityType, string entityId)
    {
        if (string.IsNullOrWhiteSpace(entityType) || string.IsNullOrWhiteSpace(entityId))
            return;

        await Groups.AddToGroupAsync(Context.ConnectionId, $"entity:{entityType}:{entityId}");

        logger.LogDebug("Connection {ConnectionId} subscribed to entity {EntityType}:{EntityId}",
            Context.ConnectionId, entityType, entityId);
    }

    /// <summary>
    /// Unsubscribe from entity-based notifications.
    /// </summary>
    public async Task UnsubscribeFromEntity(string entityType, string entityId)
    {
        if (string.IsNullOrWhiteSpace(entityType) || string.IsNullOrWhiteSpace(entityId))
            return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"entity:{entityType}:{entityId}");

        logger.LogDebug("Connection {ConnectionId} unsubscribed from entity {EntityType}:{EntityId}",
            Context.ConnectionId, entityType, entityId);
    }
}
