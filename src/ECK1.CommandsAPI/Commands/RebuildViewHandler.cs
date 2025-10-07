using ECK1.Kafka;
using MediatR;

namespace ECK1.CommandsAPI.Commands;

public interface ISampleRebuildHandler: IKafkaMessageHandler<Guid>;

public class SampleRebuildHandler(IMediator mediator) : ISampleRebuildHandler
{
    public Task Handle(string key, Guid message, KafkaMessageId messageId, CancellationToken ct) =>
        mediator.Send(new RebuildSampleViewCommand(message), ct);
}

