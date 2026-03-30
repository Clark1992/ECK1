using ECK1.CommandsAPI.Commands;
using ECK1.Reconciliation.Contracts;
using ECK1.Orleans.Grains;

namespace ECK1.CommandsAPI.Kafka;

public interface IRebuildHandler
{
    Task HandleAsync(RebuildRequest request, CancellationToken ct);
}

public class RebuildHandler<TCommand>(
    IGrainRouter<TCommand, NullGrainMetadata, ICommandResult> grainRouter,
    ILogger<RebuildHandler<TCommand>> logger) : IRebuildHandler
    where TCommand: RebuildViewCommandBase, new()
{
    public async Task HandleAsync(RebuildRequest request, CancellationToken ct)
    {
        var cmd = new TCommand
        {
            Id = request.EntityId,
            IsFullHistoryRebuild = request.IsFullHistoryRebuild,
            FailedTargets = request.FailedTargets
        };

        var res = await grainRouter.RouteToGrain(cmd, ct);

        if (res is not Success)
        {
            logger.LogWarning("Command [{TCommand}] result does not indicate success: {res}, id = {id}",
                typeof(TCommand).Name, res, request.EntityId);
        }
    }
}

