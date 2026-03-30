using ECK1.CommandsAPI.Domain;
using MediatR;

namespace ECK1.CommandsAPI.Kafka;

public record AggregateSavedNotification<TAggregate>(
    TAggregate Aggregate,
    IReadOnlyCollection<IDomainEvent> Events,
    string[] Targets = null) : INotification
    where TAggregate: IAggregateRoot;
