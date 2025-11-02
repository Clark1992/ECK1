using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace ECK1.Kafka;

public abstract class ConsumerLoop<TValue>
{
    private readonly IConsumer<string, TValue> consumer;
    private readonly ILogger logger;

    protected ConsumerLoop(
        ConsumerConfig consumerConfig,
        string topic,
        ILogger logger,
        Action<ConsumerBuilder<string, TValue>> configBuilder = null)
    {
        this.logger = logger;

        var builder = new ConsumerBuilder<string, TValue>(consumerConfig);
        configBuilder?.Invoke(builder);
        consumer = builder
            .SetErrorHandler((_, e) => logger.LogError("Kafka Error: {Error}", e))
            .Build();

        consumer.Subscribe(topic);
    }

    public Task StartConsumingAsync(
        Func<ConsumeResult<string, TValue>, Task> handler,
        CancellationToken ct) =>
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

                        try
                        {
                            await handler(result);

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
