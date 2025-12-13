namespace ECK1.IntegrationContracts.Kafka.SystemRecords;

// wip
public class ProgressStatusRecord
{
    public string InstanceId { get; set; }
    public string Topic { get; set; }
    public DateTimeOffset LastProcessedTimestamp { get; set; }
    public DateTimeOffset HeartbeatTimestamp { get; set; }
}
