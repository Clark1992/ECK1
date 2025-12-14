using ECK1.Integration.EntityStore.Generated;
using ECK1.IntegrationContracts.Abstractions;
using ECK1.Kafka;

namespace ECK1.Integration.Cache.ShortTerm.Kafka;

public class CachePopulator<TRecord>(IEntityStore store, ILogger<CachePopulator<TRecord>> logger) : IKafkaMessageHandler<TRecord>
    where TRecord: class, IIntegrationEntity
{
    public async Task Handle(string key, TRecord message, KafkaMessageId messageId, CancellationToken ct)
    {
        logger.LogInformation("Start handle 'SampleFullRecord:{id}'", message.Id);

        store.Put(message.Id, message.Version, message);

        logger.LogInformation("Handled 'SampleFullRecord:{id}'", message.Id);
    }
}
