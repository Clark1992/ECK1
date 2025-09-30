using ECK1.Kafka;
using ECK1.ReadProjector.Notifications;
using MediatR;

namespace ECK1.ReadProjector.Handlers;

public class KafkaMessageHandler<T>(IMediator mediator) : IMessageHandler<T>
    where T : class
{
    public Task Handle(T message) => mediator.Publish(new EventNotification<T>(message));
}
