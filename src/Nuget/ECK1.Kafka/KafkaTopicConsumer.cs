using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Microsoft.Extensions.Logging;

namespace ECK1.Kafka;

public interface IMessageHandler<TValue>
    where TValue : class
{
    Task Handle(TValue message);
}


public class KafkaJsonTopicConsumer<TValue>: IKafkaTopicConsumer
    where TValue : class
{
    private readonly IConsumer<string, TValue> consumer;
    private readonly IMessageHandler<TValue> handler;
    private readonly ILogger<KafkaJsonTopicConsumer<TValue>> logger;

    public KafkaJsonTopicConsumer(
        ConsumerConfig consumerConfig, 
        string topic,
        ISchemaRegistryClient schemaRegistry,
        SubjectNameStrategy strategy,
        IMessageHandler<TValue> handler,
        ILogger<KafkaJsonTopicConsumer<TValue>> logger)
    {
        this.handler = handler;
        this.logger = logger;

        var deserializerConfig = new List<KeyValuePair<string, string>>
        {
            new("json.deserializer.use.latest.version", true.ToString()),
            new("json.deserializer.subject.name.strategy", GetStrategyValue(strategy))
        };

        var jsonDeserializer = new JsonDeserializer<TValue>(schemaRegistry, deserializerConfig).AsSyncOverAsync();

        consumer = new ConsumerBuilder<string, TValue>(consumerConfig)
           .SetValueDeserializer(jsonDeserializer)
           .SetErrorHandler((_, e) => logger.LogError("Kafka Error: {Error}", e))
           .Build();

        consumer.Subscribe(topic);
    }

    public async Task StartConsumingAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(ct);
                    if (result?.Message != null)
                    {
                        try
                        {
                            await handler.Handle(result.Message.Value);
                        }
                        catch (Exception e)
                        {
                            logger.LogError(e, "Error during handling {type}", result.Message.Value.GetType().Name);
                        }
                    }
                    else
                    {
                        logger.LogWarning("result?.Message is null");
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
    }

    private static string GetStrategyValue(SubjectNameStrategy strategy) => strategy.ToString();
}

