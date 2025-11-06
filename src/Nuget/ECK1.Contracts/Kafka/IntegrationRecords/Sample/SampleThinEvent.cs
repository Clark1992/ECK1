using System.ComponentModel.DataAnnotations;

namespace ECK1.Contracts.Kafka.IntegrationRecords.Sample;

public class SampleThinEvent
{
    [Required]
    public Guid SampleId { get; set; }
    [Required]
    public int Version { get; set; }
    [Required]
    public DateTime OccuredAt { get; set; }
    [Required]
    public string EventType { get; set; }
}
