using ECK1.Kafka;
using ECK1.Orleans.Kafka;
using ECK1.ReadProjector.Notifications;
using IdentityModel;
using MediatR;

namespace ECK1.ReadProjector.Handlers;

public class KafkaMessageHandler<T>(IMediator mediator) : IKafkaMessageHandler<T>
    where T : class
{
    public Task Handle(string key, T message, long offset, CancellationToken ct) => mediator.Publish(new EventNotification<T>(message), ct);
}