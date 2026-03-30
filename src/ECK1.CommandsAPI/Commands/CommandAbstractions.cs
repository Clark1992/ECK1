using ECK1.Orleans;
using MediatR;
using Orleans;

namespace ECK1.CommandsAPI.Commands;

public record CommandRequest<TCmd, TState>(TCmd Command, TState State) : IRequest<(ICommandResult, TState)>;

[GenerateSerializer]
public class RebuildViewCommandBase : IValueId<Guid>
{
    [Id(0)]
    public Guid Id { get; set; }

    [Id(1)]
    public bool IsFullHistoryRebuild { get; set; }

    [Id(2)]
    public string[] FailedTargets { get; set; }
}