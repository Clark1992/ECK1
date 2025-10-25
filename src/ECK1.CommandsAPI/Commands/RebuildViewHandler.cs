using ECK1.Kafka;
using MediatR;

namespace ECK1.CommandsAPI.Commands;

public interface ISampleRebuildHandler: IKafkaMessageHandler<Guid>;

public class SampleRebuildHandler(IMediator mediator, ILogger<SampleRebuildHandler> logger) : ISampleRebuildHandler
{
    public async Task Handle(string key, Guid message, KafkaMessageId messageId, CancellationToken ct)
    {
        var res = await mediator.Send(new RebuildSampleViewCommand(message), ct);

        if (res is not Success)
        {
            logger.LogWarning("Command result does not indicate success: {res}, id = {id}", res, message);
        }
    }
}

