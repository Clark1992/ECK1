using Microsoft.Extensions.Hosting;

namespace ECK1.Kafka;

public class KafkaTopicConsumerService(IEnumerable<IKafkaTopicConsumer> consumers) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tasks = consumers.Select(c => c.StartConsumingAsync(stoppingToken)).ToArray();
        await Task.WhenAll(tasks);
    }
}