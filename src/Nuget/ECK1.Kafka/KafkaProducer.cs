using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace ECK1.Kafka;

public interface IKafkaProducer<T>
{
    Task ProduceAsync(string topic, T value, string key = null, CancellationToken ct = default);
    Task ProduceAsync(string topic, T value, string key, Headers headers, CancellationToken ct = default);
}

public class KafkaProducerBase<T>
    where T : class
{
    private readonly ILogger logger;
    private readonly IProducer<string, T> producer;

    public KafkaProducerBase(
        IProducer<string, T> producer,
        ILogger logger)
    {
        this.producer = producer;
        this.logger = logger;
    }

    public Task ProduceAsync(string topic, T value, string key, CancellationToken ct)
        => ProduceAsync(topic, value, key, null, ct);

    public async Task ProduceAsync(string topic, T value, string key, Headers headers, CancellationToken ct)
    {
        var message = new Message<string, T>
        {
            Key = key,
            Value = value,
            Headers = headers
        };

        try
        {
            await producer.ProduceAsync(topic, message, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during Produce.");
            throw;
        }
    }
}

public class KafkaJsonProducer<T> : KafkaProducerBase<T>, IKafkaProducer<T>
    where T : class
{
    public KafkaJsonProducer(
        IProducer<string, T> producer,
        ILogger<KafkaJsonProducer<T>> logger) : base(
            producer,
            logger)
    {
    }
}

public class KafkaAvroProducer<T> : KafkaProducerBase<T>, IKafkaProducer<T>
    where T : class
{
    public KafkaAvroProducer(
        IProducer<string, T> producer,
        ILogger<KafkaAvroProducer<T>> logger) : base(
            producer,
            logger)
    {
    }
}

public class KafkaProtoProducer<T> : KafkaProducerBase<T>, IKafkaProducer<T>
    where T : class
{
    public KafkaProtoProducer(
        IProducer<string, T> producer,
        ILogger<KafkaProtoProducer<T>> logger) : base(
            producer,
            logger)
    {
    }
}
