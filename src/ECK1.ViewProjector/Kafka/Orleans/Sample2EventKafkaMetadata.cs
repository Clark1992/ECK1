using ECK1.Orleans;
using ECK1.Orleans.Kafka;
using ECK1.ViewProjector.Events;

namespace ECK1.ViewProjector.Kafka.Orleans;

public class Sample2EventKafkaMetadata : KafkaGrainMetadata,
    IDupChecker<ISample2Event, Sample2EventKafkaMetadata>,
    IMetadataUpdater<ISample2Event, Sample2EventKafkaMetadata>,
    IFaultedStateReset<ISample2Event>
{
    public DateTimeOffset LastOccuredAt { get; set; }

    public bool IsMessageProcessed(ISample2Event entity, Sample2EventKafkaMetadata persisted)
        => entity.OccurredAt <= persisted.LastOccuredAt;

    public void Update(ISample2Event entity, Sample2EventKafkaMetadata persisted)
        => persisted.LastOccuredAt = entity.OccurredAt;

    public bool ShouldReset(ISample2Event entity) => entity is Sample2RebuiltEvent;
}
