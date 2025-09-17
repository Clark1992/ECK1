namespace ECK1.Kafka;

public interface IKafkaTopicProducer<TValue>
{
    Task ProduceAsync(TValue value, string key = null, CancellationToken ct = default);
}