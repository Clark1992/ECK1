using System.ComponentModel.DataAnnotations;

namespace ECK1.IntegrationContracts.Kafka.IntegrationRecords;

public class ThinEvent
{
    [Required]
    public Guid EventId { get; set; }
    [Required]
    public Guid EntityId { get; set; }
    [Required]
    public int Version { get; set; }
    [Required]
    public DateTime OccuredAt { get; set; }
    [Required]
    public string EventType { get; set; }
}
