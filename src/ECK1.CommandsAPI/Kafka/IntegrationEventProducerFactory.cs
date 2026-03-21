using ECK1.Integration.Config;
using ECK1.IntegrationContracts.Kafka.IntegrationRecords.Generated;
using ECK1.Kafka;

namespace ECK1.CommandsAPI.Kafka;

public interface IIntegrationEventProducerFactory
{
    IKafkaTopicProducer<ThinEvent> GetProducer<TFullRecord>();
}

public class IntegrationEventProducerFactory(IntegrationConfig config, IServiceProvider sp) : IIntegrationEventProducerFactory
{
    public IKafkaTopicProducer<ThinEvent> GetProducer<TFullRecord>()
    {
        var recordType = typeof(TFullRecord).FullName!;
        if (!config.TryGetValue(recordType, out var entry))
            throw new InvalidOperationException($"IntegrationConfig entry not found for [{recordType}].");

        return sp.GetRequiredKeyedService<IKafkaTopicProducer<ThinEvent>>(entry.EntityType);
    }
}
