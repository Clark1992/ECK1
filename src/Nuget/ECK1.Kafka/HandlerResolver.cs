using Microsoft.Extensions.DependencyInjection;

namespace ECK1.Kafka;

public interface IKafkaMessageHandler<TValue>
{
    Task Handle(string key, TValue message, KafkaMessageId messageId, CancellationToken ct);
}

public interface IKafkaHandlerResolver<TParsedValue>
{
    Func<string, TParsedValue, KafkaMessageId, CancellationToken, Task> Resolve(IServiceScope scope);
}

public class DelegateKafkaHandlerResolver<TValue>(
    Func<string, TValue, KafkaMessageId, CancellationToken, Task> func)
    : IKafkaHandlerResolver<TValue>
{
    public Func<string, TValue, KafkaMessageId, CancellationToken, Task> Resolve(IServiceScope _) => func;
}

public class TypeKafkaHandlerResolver<THandler, TValue>()
    : IKafkaHandlerResolver<TValue>
    where THandler : IKafkaMessageHandler<TValue>
{
    public Func<string, TValue, KafkaMessageId, CancellationToken, Task> Resolve(IServiceScope scope)
    {
        var handler = scope.ServiceProvider.GetRequiredService<THandler>();
        return handler.Handle;
    }
}
