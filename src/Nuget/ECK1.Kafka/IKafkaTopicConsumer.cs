namespace ECK1.Kafka;

public interface IKafkaTopicConsumer
{
    Task StartConsumingAsync(CancellationToken ct);
}
