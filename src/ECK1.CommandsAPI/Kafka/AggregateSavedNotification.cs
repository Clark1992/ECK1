using ECK1.CommandsAPI.Domain;
using MediatR;

namespace ECK1.CommandsAPI.Kafka;

public record AggregateSavedNotification<TAggregate>(
    IAggregateRoot Aggregate,
    IReadOnlyCollection<IDomainEvent> Events) : INotification;
