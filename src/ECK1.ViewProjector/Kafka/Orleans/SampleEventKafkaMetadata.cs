using ECK1.Orleans;
using ECK1.Orleans.Kafka;
using ECK1.ReadProjector.Events;

namespace ECK1.ReadProjector.Kafka.Orleans;

public class SampleEventKafkaMetadata: KafkaGrainMetadata,
    IDupChecker<ISampleEvent, SampleEventKafkaMetadata>,
    IMetadataUpdater<ISampleEvent, SampleEventKafkaMetadata>,
    IFaultedStateReset<ISampleEvent>
{
    public DateTimeOffset LastOccuredAt { get; set; }

    public bool IsMessageProcessed(ISampleEvent entity, SampleEventKafkaMetadata persisted)
        => entity.OccurredAt <= persisted.LastOccuredAt;

    public void Update(ISampleEvent entity, SampleEventKafkaMetadata persisted)
        => persisted.LastOccuredAt = entity.OccurredAt;

    public bool ShouldReset(ISampleEvent entity) => entity is SampleRebuiltEvent;
}
