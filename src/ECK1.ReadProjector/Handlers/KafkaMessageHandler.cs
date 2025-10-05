using ECK1.Kafka;
using ECK1.ReadProjector.Notifications;
using MediatR;

namespace ECK1.ReadProjector.Handlers;

public class KafkaMessageHandler<T>(IMediator mediator) : IKafkaMessageHandler<T>
    where T : class
{
    public Task Handle(string key, T message, KafkaMessageId messageId, CancellationToken ct) => mediator.Publish(new EventNotification<T>(message), ct);
}