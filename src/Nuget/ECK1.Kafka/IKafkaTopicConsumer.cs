namespace ECK1.Kafka;

public interface IKafkaTopicConsumer
{
    Task StartConsumingAsync(CancellationToken ct);
}

internal interface IHandlerConfigurator<TValue>
{
    IKafkaTopicConsumer WithHandler<THandler>() where THandler: IKafkaMessageHandler<TValue>;
    IKafkaTopicConsumer WithHandler(Func<string, TValue, KafkaMessageId, CancellationToken, Task> handler);
}