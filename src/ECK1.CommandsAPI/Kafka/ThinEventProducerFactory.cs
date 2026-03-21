using ECK1.Integration.Config;
using ECK1.IntegrationContracts.Kafka.IntegrationRecords.Generated;
using ECK1.Kafka;

namespace ECK1.CommandsAPI.Kafka;

public class ThinEventProducerFactory(IntegrationConfig config, IServiceProvider sp)
{
    public IKafkaTopicProducer<ThinEvent> GetProducer<TRecord>()
    {
        var recordType = typeof(TRecord).FullName!;
        if (!config.TryGetValue(recordType, out var entry))
            throw new InvalidOperationException($"IntegrationConfig entry not found for [{recordType}].");

        return sp.GetRequiredKeyedService<IKafkaTopicProducer<ThinEvent>>(entry.EntityType);
    }
}
