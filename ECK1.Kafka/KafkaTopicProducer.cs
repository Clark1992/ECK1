using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using System.Text.Json;

namespace ECK1.Kafka;

public class KafkaJsonTopicProducer<TValue> : IKafkaTopicProducer<TValue>
    where TValue : class
{
    private readonly IProducer<string, TValue> _producer;
    private readonly string _topic;
    private readonly JsonSerializerOptions options;

    public KafkaJsonTopicProducer(
        Handle rootHandle,
        string topic,
        ISchemaRegistryClient schemaRegistry)
    {
        _producer = new DependentProducerBuilder<string, TValue>(rootHandle)
            .SetValueSerializer(new JsonSerializer<TValue>(
                schemaRegistry,
                new JsonSerializerConfig
                {
                    AutoRegisterSchemas = false,
                    UseLatestVersion = true,
                }
            ))
            .Build();

        _topic = topic;
    }

    public async Task ProduceAsync(TValue value, string key = null, CancellationToken ct = default)
    {
        var message = new Message<string, TValue>
        {
            Key = key ?? Guid.NewGuid().ToString(),
            Value = value
        };

        await _producer.ProduceAsync(_topic, message, ct);
    }
}

public class KafkaAvroTopicProducer<TValue> : IKafkaTopicProducer<TValue>
{
    private readonly IProducer<string, TValue> _producer;
    private readonly string _topic;

    public KafkaAvroTopicProducer(Handle rootHandle, string topic, ISchemaRegistryClient schemaRegistry)
    {
        _producer = new DependentProducerBuilder<string, TValue>(rootHandle)
            .SetValueSerializer(new AvroSerializer<TValue>(schemaRegistry, new ()
            {
                AutoRegisterSchemas = false,
                UseLatestVersion = true
            }))
            .Build();

        _topic = topic;
    }

    public async Task ProduceAsync(TValue value, string key = null, CancellationToken ct = default)
    {
        var message = new Message<string, TValue>
        {
            Key = key ?? Guid.NewGuid().ToString(),
            Value = value
        };

        await _producer.ProduceAsync(_topic, message, ct);
    }
}
