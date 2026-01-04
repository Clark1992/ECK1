namespace ECK1.FailedViewRebuilder;

public class KafkaSettings
{
    public static string Section => "Kafka";
    public string BootstrapServers { get; set; }
    public string SchemaRegistryUrl { get; set; }
    public string User { get; set; }
    public string Secret { get; set; }
    public string SampleFailureEventsTopic { get; set; }
    public string SampleEventsRebuildRequestTopic { get; set; }
    public string Sample2FailureEventsTopic { get; set; }
    public string Sample2EventsRebuildRequestTopic { get; set; }
    public string GroupId { get; set; }
}
