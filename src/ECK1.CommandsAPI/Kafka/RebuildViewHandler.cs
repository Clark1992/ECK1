using ECK1.CommandsAPI.Commands;
using ECK1.Kafka;
using MediatR;

namespace ECK1.CommandsAPI.Kafka;

public interface IRebuildHandler: IKafkaMessageHandler<Guid>;

public class RebuildHandler<TCommand>(IMediator mediator, ILogger<RebuildHandler<TCommand>> logger) : IRebuildHandler
    where TCommand: IRebuildViewCommandBase, new()
{
    public async Task Handle(string key, Guid message, KafkaMessageId messageId, CancellationToken ct)
    {
        var cmd = new TCommand
        {
            Id = message
        };
        var res = await mediator.Send(cmd, ct);

        if (res is not Success)
        {
            logger.LogWarning("Command [{TCommand}] result does not indicate success: {res}, id = {id}", typeof(TCommand).Name, res, message);
        }
    }
}

