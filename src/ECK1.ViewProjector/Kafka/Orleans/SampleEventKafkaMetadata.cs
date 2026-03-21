using ECK1.Orleans;
using ECK1.Orleans.Grains;
using ECK1.ViewProjector.Events;

namespace ECK1.ViewProjector.Kafka.Orleans;

public class SampleEventKafkaMetadata: GrainMetadata,
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
