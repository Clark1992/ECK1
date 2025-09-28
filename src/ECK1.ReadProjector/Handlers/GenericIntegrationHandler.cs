using ECK1.Kafka;
using MediatR;

namespace ECK1.ReadProjector.Handlers;

public class GenericIntegrationHandler<T>(IMediator mediator) : IMessageHandler<T>
    where T : class
{
    public Task Handle(T message) => mediator.Publish(message);
}
