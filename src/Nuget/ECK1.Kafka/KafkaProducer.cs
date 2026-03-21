using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace ECK1.Kafka;

public interface IKafkaProducer
{
    Task ProduceAsync(string topic, object value, string key = null, CancellationToken ct = default);
}

public class KafkaProducerBase
{
    private readonly ILogger logger;
    private readonly IProducer<string, object> producer;

    public KafkaProducerBase(
        IProducer<string, object> producer,
        ILogger<IKafkaProducer> logger)
    {
        this.producer = producer;
        this.logger = logger;
    }

    public async Task ProduceAsync(string topic, object value, string key, CancellationToken ct)
    {
        var message = new Message<string, object>
        {
            Key = key,
            Value = value
        };

        try
        {
            await producer.ProduceAsync(topic, message, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during Produce.");
        }
    }
}

public class KafkaJsonProducer : KafkaProducerBase, IKafkaProducer
{
    public KafkaJsonProducer(
        IProducer<string, object> producer,
        ILogger<KafkaJsonProducer> logger) : base(
            producer,
            logger)
    {
    }
}

public class KafkaAvroProducer : KafkaProducerBase, IKafkaProducer
{
    public KafkaAvroProducer(
        IProducer<string, object> producer,
        ILogger<KafkaAvroProducer> logger) : base(
            producer,
            logger)
    {
    }
}

public class KafkaProtoProducer : KafkaProducerBase, IKafkaProducer
{
    public KafkaProtoProducer(
        IProducer<string, object> producer,
        ILogger<KafkaProtoProducer> logger) : base(
            producer,
            logger)
    {
    }
}
