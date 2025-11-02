namespace ECK1.Kafka;

public interface IKafkaTopicConsumer
{
    Task StartConsumingAsync(CancellationToken ct);
}

public interface IHandlerConfigurator<TValue>: IKafkaTopicConsumer
{
    IHandlerConfigurator<TValue> WithHandler<THandler>() where THandler: IKafkaMessageHandler<TValue>;
    IHandlerConfigurator<TValue> WithHandler(Func<string, TValue, KafkaMessageId, CancellationToken, Task> handler);
}

public interface IParserConfigurator<TValue> : IKafkaTopicConsumer
{
    IParserConfigurator<TValue> WithParser(Func<string, TValue> parser);
}
