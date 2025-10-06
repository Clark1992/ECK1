using ECK1.Kafka;
using ECK1.ViewProjector.Notifications;
using MediatR;

namespace ECK1.ViewProjector.Handlers;

public class KafkaMessageHandler<T>(IMediator mediator) : IKafkaMessageHandler<T>
    where T : class
{
    public Task Handle(string key, T message, KafkaMessageId messageId, CancellationToken ct) => mediator.Publish(new EventNotification<T>(message), ct);
}