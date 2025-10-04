namespace ECK1.Kafka;

public interface IKafkaTopicConsumer
{
    Task StartConsumingAsync(CancellationToken ct);
}

internal interface IHandlerConfigurator<TValue>
    where TValue : class
{
    IKafkaTopicConsumer WithHandler(IKafkaMessageHandler<TValue> handler);

    IKafkaTopicConsumer WithHandler(Func<string, TValue, long, CancellationToken, Task> handler);
}