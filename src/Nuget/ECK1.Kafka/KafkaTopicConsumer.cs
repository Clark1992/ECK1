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

public interface IKafkaMessageHandler<TValue>
{
    Task Handle(string key, TValue message, KafkaMessageId messageId, CancellationToken ct);
}

#region Handler resolvers

public interface IKafkaHandlerResolver<TParsedValue>
{
    Func<string, TParsedValue, KafkaMessageId, CancellationToken, Task> Resolve(IServiceScope scope);
}

public class DelegateKafkaHandlerResolver<TParsedValue>(
    Func<string, TParsedValue, KafkaMessageId, CancellationToken, Task> func)
    : IKafkaHandlerResolver<TParsedValue>
{
    public Func<string, TParsedValue, KafkaMessageId, CancellationToken, Task> Resolve(IServiceScope _) => func;
}

public class TypeKafkaHandlerResolver<THandler, TParsedValue>()
    : IKafkaHandlerResolver<TParsedValue>
    where THandler : IKafkaMessageHandler<TParsedValue>
{
    public Func<string, TParsedValue, KafkaMessageId, CancellationToken, Task> Resolve(IServiceScope scope)
    {
        var handler = scope.ServiceProvider.GetRequiredService<THandler>();
        return handler.Handle;
    }
}

#endregion

#region Consumers

public abstract class KafkaConsumerBase<TValue, TParsedValue> : IKafkaTopicConsumer, IHandlerConfigurator<TParsedValue>
{
    private readonly IConsumer<string, TValue> consumer;
    private readonly ILogger logger;
    private readonly IServiceScopeFactory scopeFactory;
    private IKafkaHandlerResolver<TParsedValue> handlerResolver;

    protected KafkaConsumerBase(
        ConsumerConfig consumerConfig,
        string topic,
        ILogger logger,
        IServiceScopeFactory scopeFactory,
        Func<ConsumerBuilder<string, TValue>, ConsumerBuilder<string, TValue>> configBuilder = null)
    {
        this.logger = logger;
        this.scopeFactory = scopeFactory;

        var builder = new ConsumerBuilder<string, TValue>(consumerConfig);
        builder = configBuilder?.Invoke(builder);
        consumer = builder
            .SetErrorHandler((_, e) => logger.LogError("Kafka Error: {Error}", e))
            .Build();

        consumer.Subscribe(topic);
    }

    protected abstract TParsedValue GetValue(TValue value);

    public IKafkaTopicConsumer WithHandler(Func<string, TParsedValue, KafkaMessageId, CancellationToken, Task> handler)
    {
        handlerResolver = new DelegateKafkaHandlerResolver<TParsedValue>(handler);
        return this;
    }

    public IKafkaTopicConsumer WithHandler<THandler>()
        where THandler : IKafkaMessageHandler<TParsedValue>
    {
        handlerResolver = new TypeKafkaHandlerResolver<THandler, TParsedValue>();
        return this;
    }

    public Task StartConsumingAsync(CancellationToken ct) =>
        Task.Run(async () =>
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        var result = consumer.Consume(ct);
                        if (result?.Message == null)
                            continue;

                        using var scope = scopeFactory.CreateScope();
                        var handler = handlerResolver.Resolve(scope);
                        try
                        {
                            await handler(
                                result.Message.Key,
                                GetValue(result.Message.Value),
                                new KafkaMessageId(result.TopicPartitionOffset),
                                ct);

                            consumer.Commit(result);
                        }
                        catch (Exception e)
                        {
                            logger.LogError(e, "Error during handling {type}", result.Message.Value.GetType().Name);
                        }
                    }
                    catch (ConsumeException ex)
                    {
                        logger.LogError(ex, "Consume error: {Reason}", ex.Error.Reason);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
            finally
            {
                consumer.Close();
            }
        }, ct);
}

public abstract class KafkaConsumerBase<TValue>(
    ConsumerConfig consumerConfig,
    string topic,
    ILogger logger,
    IServiceScopeFactory scopeFactory,
    Func<ConsumerBuilder<string, TValue>, ConsumerBuilder<string, TValue>> configBuilder = null)
    : KafkaConsumerBase<TValue, TValue>(
        consumerConfig,
        topic,
        logger,
        scopeFactory,
        configBuilder)
{
    protected override TValue GetValue(TValue value) => value;
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
            return builder.SetValueDeserializer(jsonDeserializer);
        })
    where TValue : class
{ }

public class KafkaSimpleTopicConsumer<TValue>(
    ConsumerConfig consumerConfig,
    string topic,
    Func<string, TValue> parser,
    ILogger<KafkaSimpleTopicConsumer<TValue>> logger,
    IServiceScopeFactory scopeFactory) :
    KafkaConsumerBase<string, TValue>(consumerConfig, topic, logger, scopeFactory)
{
    protected override TValue GetValue(string value) => parser(value);
}

#endregion
