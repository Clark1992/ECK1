using ECK1.Kafka;
using Microsoft.Extensions.Hosting;

public class KafkaTopicConsumerService : BackgroundService
{
    private readonly IEnumerable<IKafkaTopicConsumer> consumers;

    public KafkaTopicConsumerService(
        IEnumerable<IKafkaTopicConsumer> consumers)
    {
        this.consumers = consumers;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tasks = consumers.Select(c => c.StartConsumingAsync(stoppingToken)).ToArray();
        await Task.WhenAll(tasks);
    }
}
