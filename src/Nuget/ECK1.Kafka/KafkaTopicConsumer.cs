using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;

namespace ECK1.Kafka;

public interface IKafkaMessageHandler<TValue>
    where TValue : class
{
    Task Handle(string key, TValue message, long offset, CancellationToken ct);
}


public class KafkaJsonTopicConsumer<TValue>: IKafkaTopicConsumer, IHandlerConfigurator<TValue>
    where TValue : class
{
    private readonly IConsumer<string, TValue> consumer;
    private IKafkaMessageHandler<TValue> handler;
    private Func<string, TValue, long, CancellationToken, Task> handlerFunc;
    private readonly ILogger<KafkaJsonTopicConsumer<TValue>> logger;

    private Func<string, TValue, long, CancellationToken, Task> Handler => this.handler is not null ?
        this.handler.Handle :
        this.handlerFunc ?? throw new InvalidOperationException("Handler not set");

    public KafkaJsonTopicConsumer(
        ConsumerConfig consumerConfig, 
        string topic,
        ISchemaRegistryClient schemaRegistry,
        SubjectNameStrategy strategy,
        ILogger<KafkaJsonTopicConsumer<TValue>> logger)
    {
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

    public IKafkaTopicConsumer WithHandler(IKafkaMessageHandler<TValue> handler)
    {
        this.handler = handler;
        this.handlerFunc = null;

        return this;
    }

    public IKafkaTopicConsumer WithHandler(Func<string, TValue, long, CancellationToken, Task> handler)
    {
        this.handler = null;
        this.handlerFunc = handler;

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
                        if (result?.Message != null)
                        {
                            try
                            {
                                await Handler(result.Message.Key, result.Message.Value, result.Offset.Value, ct);

                                consumer.Commit(result);
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
        }, ct);

    private static string GetStrategyValue(SubjectNameStrategy strategy) => strategy.ToString();
}

