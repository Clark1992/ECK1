using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ECK1.Kafka;

public class KafkaMessageId(TopicPartitionOffset o)
{
    public string Topic { get; set; } = o.Topic;
    public int Partition { get; set; } = o.Partition;
    public long Offset { get; set; } = o.Offset;
}

#region Consumers

public abstract class KafkaConsumerBase<TValue>(
    ConsumerConfig consumerConfig,
    string topic,
    ILogger logger,
    IServiceScopeFactory scopeFactory,
    Action<ConsumerBuilder<string, TValue>> configBuilder = null) : ConsumerLoop<TValue>(
        consumerConfig, topic, logger, configBuilder), IHandlerConfigurator<TValue>
{
    private IKafkaHandlerResolver<TValue> handlerResolver;

    public Task StartConsumingAsync(CancellationToken ct) =>
        base.StartConsumingAsync(async result =>
        {
            using var scope = scopeFactory.CreateScope();
            var handler = handlerResolver.Resolve(scope);

            await handler(
                result.Message.Key,
                result.Message.Value,
                new KafkaMessageId(result.TopicPartitionOffset),
                ct);

        }, ct);

    public IHandlerConfigurator<TValue> WithHandler(Func<string, TValue, KafkaMessageId, CancellationToken, Task> handler)
    {
        handlerResolver = new DelegateKafkaHandlerResolver<TValue>(handler);
        return this;
    }

    public IHandlerConfigurator<TValue> WithHandler<THandler>()
        where THandler : IKafkaMessageHandler<TValue>
    {
        handlerResolver = new TypeKafkaHandlerResolver<THandler, TValue>();
        return this;
    }
}


public abstract class KafkaSimpleConsumerBase<TValue>(
    ConsumerConfig consumerConfig,
    string topic,
    ILogger logger,
    IServiceScopeFactory scopeFactory,
    Action<ConsumerBuilder<string, string>> configBuilder = null) : ConsumerLoop<string>(
        consumerConfig, topic, logger, configBuilder), IHandlerConfigurator<TValue>, IParserConfigurator<TValue>
{
    private IKafkaHandlerResolver<TValue> handlerResolver;
    private Func<string, TValue> parser;

    public Task StartConsumingAsync(CancellationToken ct) =>
        base.StartConsumingAsync(async result =>
        {
            using var scope = scopeFactory.CreateScope();
            var handler = handlerResolver.Resolve(scope);

            if (parser is null)
            {
                throw new InvalidOperationException("Parser not specified for simple string consumer");
            }

            await handler(
                result.Message.Key,
                parser(result.Message.Value),
                new KafkaMessageId(result.TopicPartitionOffset),
                ct);

        }, ct);

    public IHandlerConfigurator<TValue> WithHandler(Func<string, TValue, KafkaMessageId, CancellationToken, Task> handler)
    {
        handlerResolver = new DelegateKafkaHandlerResolver<TValue>(handler);
        return this;
    }

    public IHandlerConfigurator<TValue> WithHandler<THandler>()
        where THandler : IKafkaMessageHandler<TValue>
    {
        handlerResolver = new TypeKafkaHandlerResolver<THandler, TValue>();
        return this;
    }

    public IParserConfigurator<TValue> WithParser(Func<string, TValue> parser)
    {
        this.parser = parser;
        return this;
    }
}

#endregion

#region Specific consumers

public class KafkaJsonTopicConsumer<TValue>(
    ConsumerConfig consumerConfig,
    string topic,
    ISchemaRegistryClient schemaRegistry,
    SubjectNameStrategy strategy,
    ILogger<KafkaJsonTopicConsumer<TValue>> logger,
    IServiceScopeFactory scopeFactory)
    : KafkaConsumerBase<TValue>(
        consumerConfig,
        topic,
        logger,
        scopeFactory,
        builder =>
        {
            var deserializerConfig = new List<KeyValuePair<string, string>>
            {
                new("json.deserializer.use.latest.version", "true"),
                new("json.deserializer.subject.name.strategy", strategy.ToString())
            };
            var jsonDeserializer = new JsonDeserializer<TValue>(schemaRegistry, deserializerConfig).AsSyncOverAsync();
            builder.SetValueDeserializer(jsonDeserializer);
        })
    where TValue : class
{ }

public class KafkaSimpleTopicConsumer<TValue>(
    ConsumerConfig consumerConfig,
    string topic,
    ILogger<KafkaSimpleTopicConsumer<TValue>> logger,
    IServiceScopeFactory scopeFactory) :
    KafkaSimpleConsumerBase<TValue>(consumerConfig, topic, logger, scopeFactory)
{
}

public class KafkaAvroTopicConsumer<TValue>(
    ConsumerConfig consumerConfig,
    string topic,
    ISchemaRegistryClient schemaRegistry,
    SubjectNameStrategy strategy,
    ILogger<KafkaAvroTopicConsumer<TValue>> logger,
    IServiceScopeFactory scopeFactory)
    : KafkaConsumerBase<TValue>(
        consumerConfig,
        topic,
        logger,
        scopeFactory,
        builder =>
        {
            var deserializerConfig = new AvroDeserializerConfig
            {
                UseLatestVersion = true,
                SubjectNameStrategy = strategy
            };
            var jsonDeserializer = new AvroDeserializer<TValue>(schemaRegistry, deserializerConfig).AsSyncOverAsync();
            builder.SetValueDeserializer(jsonDeserializer);
        })
    where TValue : class
{ }

#endregion
