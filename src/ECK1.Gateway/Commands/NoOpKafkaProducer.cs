using Confluent.Kafka;
using ECK1.Kafka;

namespace ECK1.Gateway.Commands;

/// <summary>
/// Fallback producer when Kafka is not configured (local development).
/// </summary>
internal class NoOpKafkaProducer<T>(ILogger<NoOpKafkaProducer<T>> logger) : IKafkaProducer<T>
    where T : class
{
    public Task ProduceAsync(string topic, T value, string key, CancellationToken ct)
    {
        logger.LogWarning("Kafka not configured. Would publish to {Topic} with key {Key}", topic, key);
        return Task.CompletedTask;
    }

    public Task ProduceAsync(string topic, T value, string key, Headers headers, CancellationToken ct)
        => ProduceAsync(topic, value, key, ct);
}
