using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ECK1.Kafka;

public abstract class ConsumerLoop<TValue>
{
    private readonly IConsumer<string, TValue> consumer;
    protected readonly ILogger Logger;

    protected ConsumerLoop(
        ConsumerConfig consumerConfig,
        string topic,
        ILogger logger,
        Action<ConsumerBuilder<string, TValue>> configBuilder = null)
    {
        Logger = logger;

        var builder = new ConsumerBuilder<string, TValue>(consumerConfig);
        configBuilder?.Invoke(builder);
        consumer = builder
            .SetErrorHandler((_, e) => Logger.LogError("Kafka Error: {Error}", e))
            .Build();

        consumer.Subscribe(topic);
    }

    public Task StartConsumingAsync(
        Func<ConsumeResult<string, TValue>, Task> handler,
        CancellationToken ct) =>
        Task.Run(() =>
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        consumer.ConsumeWithInstrumentation(result =>
                        {
                            if (result?.Message == null)
                            {
                                return;
                            }

                            KafkaBaggagePropagation.ExtractBaggage(result.Message.Headers);

                            try
                            {
                                handler(result).GetAwaiter().GetResult();
                                consumer.Commit(result);
                            }
                            catch (Exception e)
                            {
                                Logger.LogError(e, "Error during handling {type}", result.Message.Value.GetType().Name);
                            }
                        }, 1000);
                    }
                    catch (ConsumeException ex)
                    {
                        Logger.LogError(ex, "Consume error: {Reason}", ex.Error.Reason);
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
