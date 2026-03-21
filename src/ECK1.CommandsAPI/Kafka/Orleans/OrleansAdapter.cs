using System.Collections.Concurrent;
using System.Linq.Expressions;
using ECK1.CommandsAPI.Commands;
using ECK1.Kafka;
using ECK1.Orleans;
using ECK1.Orleans.Grains;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace ECK1.CommandsAPI.Kafka.Orleans;

public class OrleansAdapter<TOrleansSerializableValue, TMetadata, TResult>(
    IGrainRouter<TOrleansSerializableValue, TMetadata, TResult> router) : IKafkaMessageHandler<TOrleansSerializableValue>
    where TOrleansSerializableValue : class
{
    public Task Handle(string key, TOrleansSerializableValue message, KafkaMessageId _, CancellationToken ct) =>
        router.RouteToGrain(message, ct);
}

public class CommandGrainHandler<TCmd, TState>(IServiceScopeFactory scopeFactory, ILogger<CommandGrainHandler<TCmd, TState>> logger)
    : IStatefulGrainHandler<TCmd, TState, ICommandResult>
    where TCmd : class, IRequest<(ICommandResult, TState)>
{
    private static readonly ConcurrentDictionary<Type, Func<TCmd, TState, object>> RequestFactories = new();

    public async Task<(ICommandResult, TState)> Handle(TCmd cmd, TState state, CancellationToken ct)
    { 
        var type = cmd.GetType();
        logger.LogInformation("Handling {messageType}", type);

        await using var scope = scopeFactory.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var request = CreateTypedRequest(cmd, state);
        var result = await mediator.Send(request, ct);

        logger.LogInformation("Handled {messageType}", type);

        return ((ICommandResult, TState))result;
    }

    private static object CreateTypedRequest(TCmd cmd, TState state)
    {
        var factory = RequestFactories.GetOrAdd(cmd.GetType(), static concreteType =>
        {
            var requestType = typeof(CommandRequest<,>).MakeGenericType(concreteType, typeof(TState));
            var ctor = requestType.GetConstructors()[0];

            var cmdParam = Expression.Parameter(typeof(TCmd), "cmd");
            var stateParam = Expression.Parameter(typeof(TState), "state");

            var body = Expression.New(ctor, Expression.Convert(cmdParam, concreteType), stateParam);

            return Expression.Lambda<Func<TCmd, TState, object>>(body, cmdParam, stateParam).Compile();
        });

        return factory(cmd, state);
    }
}