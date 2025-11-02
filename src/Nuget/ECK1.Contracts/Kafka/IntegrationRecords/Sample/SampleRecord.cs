using ECK1.Contracts.Kafka.BusinessEvents.Sample;

namespace ECK1.Contracts.Kafka.IntegrationRecords.Sample;

public record SampleFullRecord
{
    public Guid SampleId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public SampleAddress Address { get; set; }
    public List<SampleAttachment> Attachments { get; set; }
    public int Version { get; set; }
    public DateTime OccuredAt { get; set; }
}

public record SampleThinRecordThin(Guid SampleId, int Version, DateTime OccuredAt, string EventType);
