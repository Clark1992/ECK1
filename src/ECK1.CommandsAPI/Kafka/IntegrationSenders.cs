using AutoMapper;
using ECK1.CommandsAPI.Domain;
using ECK1.IntegrationContracts.Abstractions;
using ECK1.IntegrationContracts.Kafka.IntegrationRecords.Generated;
using ECK1.Kafka;
using ECK1.VersionTracker.Contracts;
using MediatR;

namespace ECK1.CommandsAPI.Kafka;

public class IntegrationSender<TAggregateRoot, TFullRecord>(
    IMapper mapper,
    IKafkaTopicProducer<TFullRecord> recordProducer,
    IIntegrationEventProducerFactory integartionEventProducerFactory,
    ILogger<IntegrationSender<TAggregateRoot, TFullRecord>> _)
    : INotificationHandler<AggregateSavedNotification<TAggregateRoot>>
    where TAggregateRoot: IAggregateRoot, IAggregateRootInternal
    where TFullRecord: IIntegrationMessage
{
    private readonly IKafkaTopicProducer<ThinEvent> thinEventProducer = integartionEventProducerFactory.GetProducer<TFullRecord>();

    public async Task Handle(AggregateSavedNotification<TAggregateRoot> notification, CancellationToken ct)
    {
        // Normalize targets: [VersionTracker] means "only VersionTracker failed",
        // so all integration targets should run → convert to [] (= ALL).
        var targets = notification.Targets;
        if (targets is [VersionTrackerConstants.TargetName])
            targets = [];

        var untouched = notification.Aggregate.Untouched;
        var indexedEvents = notification.Events.OrderBy(x => x.Version).ToList();
        var key = notification.Aggregate.Id.ToString();

        foreach (var domainEvent in indexedEvents)
        {
            untouched.ReplayEvent(domainEvent);

            var thinEvent = new ThinEvent
            {
                EventId = domainEvent.EventId,
                EntityId = notification.Aggregate.Id,
                EventType = domainEvent.GetType().FullName,
                OccuredAt = domainEvent.OccurredAt.UtcDateTime,
                Version = domainEvent.Version,
                Targets = targets ?? []
            };

            var fullRecord = mapper.Map<TFullRecord>(untouched);
            fullRecord.OccuredAt = thinEvent.OccuredAt;

            await recordProducer.ProduceAsync(fullRecord, key, ct);
            await thinEventProducer.ProduceAsync(thinEvent, key, ct);
        }
    }
}
