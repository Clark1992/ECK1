using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Microsoft.Extensions.Logging;

namespace ECK1.Kafka;

public class KafkaTopicProducerBase<TValue> where TValue : class
{
    private readonly ILogger logger;
    private readonly IProducer<string, TValue> producer;
    private readonly string topic;

    public KafkaTopicProducerBase(
        Handle rootHandle, 
        string topic,
        IAsyncSerializer<TValue> asyncSerializer,
        ILogger<IKafkaTopicProducer<TValue>> logger)
    {
        producer = new DependentProducerBuilder<string, TValue>(rootHandle)
            .SetValueSerializer(asyncSerializer)
            .Build();

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
        Handle rootHandle,
        string topic,
        ISchemaRegistryClient schemaRegistry,
        SubjectNameStrategy strategy,
        ILogger<KafkaJsonTopicProducer<TValue>> logger): base(
            rootHandle, 
            topic, 
            new JsonSerializer<TValue>(
                schemaRegistry,
                new JsonSerializerConfig
                {
                    AutoRegisterSchemas = false,
                    UseLatestVersion = true,
                    SubjectNameStrategy = strategy
                }
            ),
            logger)
    {
    }
}

public class KafkaAvroTopicProducer<TValue> : KafkaTopicProducerBase<TValue>, IKafkaTopicProducer<TValue>
    where TValue : class
{
    public KafkaAvroTopicProducer(
        Handle rootHandle,
        string topic,
        ISchemaRegistryClient schemaRegistry,
        SubjectNameStrategy strategy,
        ILogger<KafkaAvroTopicProducer<TValue>> logger) : base(
            rootHandle,
            topic,
            new AvroSerializer<TValue>(schemaRegistry, new()
            {
                AutoRegisterSchemas = false,
                UseLatestVersion = true,
                SubjectNameStrategy = strategy
            }),
            logger)
    {
    }
}

public interface IKafkaSimpleProducer<TValue>
{
    Task ProduceAsync(TValue value, string topic, string key, CancellationToken ct);

    Task ProduceAsync(TValue value, string topic, CancellationToken ct);
}

public class KafkaSimpleProducer<TValue> : IKafkaSimpleProducer<TValue>
{
    private readonly ILogger<KafkaSimpleProducer<TValue>> logger;
    private readonly IProducer<string, string> producer;

    public KafkaSimpleProducer(
        Handle rootHandle,
        ILogger<KafkaSimpleProducer<TValue>> logger)
    {
        producer = new DependentProducerBuilder<string, string>(rootHandle)
            .Build();

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