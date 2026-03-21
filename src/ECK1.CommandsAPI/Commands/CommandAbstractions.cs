using ECK1.Orleans;
using MediatR;

namespace ECK1.CommandsAPI.Commands;

public record CommandRequest<TCmd, TState>(TCmd Command, TState State) : IRequest<(ICommandResult, TState)>;

public class RebuildViewCommandBase : IValueId<Guid>
{
    public Guid Id { get; set; }
}