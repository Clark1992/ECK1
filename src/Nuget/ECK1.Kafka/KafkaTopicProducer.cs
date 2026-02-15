using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace ECK1.Kafka;

public class KafkaTopicProducerBase<TValue> where TValue : class
{
    private readonly ILogger logger;
    private readonly IProducer<string, TValue> producer;
    private readonly string topic;

    public KafkaTopicProducerBase(
        IProducer<string, TValue> producer,
        string topic,
        ILogger<IKafkaTopicProducer<TValue>> logger)
    {
        this.producer = producer;
        this.topic = topic;
        this.logger = logger;
    }

    public async Task ProduceAsync(TValue value, string key, CancellationToken ct)
    {
        var message = new Message<string, TValue>
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

public class KafkaJsonTopicProducer<TValue> : KafkaTopicProducerBase<TValue>, IKafkaTopicProducer<TValue>
    where TValue : class
{
    public KafkaJsonTopicProducer(
        IProducer<string, TValue> producer,
        string topic,
        ILogger<KafkaJsonTopicProducer<TValue>> logger) : base(
            producer,
            topic,
            logger)
    {
    }
}

public class KafkaAvroTopicProducer<TValue> : KafkaTopicProducerBase<TValue>, IKafkaTopicProducer<TValue>
    where TValue : class
{
    public KafkaAvroTopicProducer(
        IProducer<string, TValue> producer,
        string topic,
        ILogger<KafkaAvroTopicProducer<TValue>> logger) : base(
            producer,
            topic,
            logger)
    {
    }
}

public class KafkaProtoTopicProducer<TValue> : KafkaTopicProducerBase<TValue>, IKafkaTopicProducer<TValue>
    where TValue : class
{
    public KafkaProtoTopicProducer(
        IProducer<string, TValue> producer,
        string topic,
        ILogger<KafkaProtoTopicProducer<TValue>> logger) : base(
            producer,
            topic,
            logger)
    {
    }
}

public interface IKafkaSimpleProducer<TValue>
{
    Task ProduceAsync(TValue value, string topic, string key, CancellationToken ct);

    Task ProduceAsync(TValue value, string topic, CancellationToken ct);
}

public interface IKafkaRawBytesProducer
{
    Task ProduceAsync(byte[] value, string topic, string key, CancellationToken ct);
}

public class KafkaRawBytesProducer : IKafkaRawBytesProducer
{
    private readonly ILogger logger;
    private readonly IProducer<string, byte[]> producer;

    public KafkaRawBytesProducer(
        IProducer<string, byte[]> producer,
        ILogger logger)
    {
        this.producer = producer;
        this.logger = logger;
    }

    public Task ProduceAsync(byte[] value, string topic, CancellationToken ct) =>
        ProduceAsync(value, topic, value.ToString(), ct);

    public async Task ProduceAsync(byte[] value, string topic, string key, CancellationToken ct)
    {
        var message = new Message<string, byte[]>
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


public class KafkaSimpleProducer<TValue> : IKafkaSimpleProducer<TValue>
{
    private readonly ILogger<KafkaSimpleProducer<TValue>> logger;
    private readonly IProducer<string, string> producer;

    public KafkaSimpleProducer(
        IProducer<string, string> producer,
        ILogger<KafkaSimpleProducer<TValue>> logger)
    {
        this.producer = producer;
        this.logger = logger;
    }

    public Task ProduceAsync(TValue value, string topic, CancellationToken ct) =>
        ProduceAsync(value, topic, value.ToString(), ct);

    public async Task ProduceAsync(TValue value, string topic, string key, CancellationToken ct)
    {
        var message = new Message<string, string>
        {
            Key = key,
            Value = value.ToString()
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