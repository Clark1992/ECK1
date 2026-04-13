using ECK1.CommandsAPI.Domain;
using ECK1.VersionTracker.Contracts;
using MediatR;

namespace ECK1.CommandsAPI.Kafka;

public class VersionNotifier<TAggregate>(
    IVersionTrackerService versionTracker,
    ILogger<VersionNotifier<TAggregate>> logger)
    : INotificationHandler<AggregateSavedNotification<TAggregate>>
    where TAggregate : IAggregateRoot
{
    public async Task Handle(AggregateSavedNotification<TAggregate> notification, CancellationToken ct)
    {
        var aggregate = notification.Aggregate;
        var entityType = $"ECK1.{typeof(TAggregate).Name}";

        try
        {
            await versionTracker.PutVersion(new PutVersionRequest
            {
                EntityType = entityType,
                EntityId = aggregate.Id.ToString(),
                Version = aggregate.Version
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to put version for {EntityType}:{EntityId}",
                entityType, aggregate.Id);
        }
    }
}
