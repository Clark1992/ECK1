namespace ECK1.ViewProjector;

public class KafkaSettings
{
    public static string Section => "Kafka";
    public string BootstrapServers { get; set; }
    public string SchemaRegistryUrl { get; set; }
    public string User { get; set; }
    public string Secret { get; set; }
    public string FailureEventsTopic { get; set; }
    public string SampleBusinessEventsTopic { get; set; }
    public string SampleFullRecordsTopic { get; set; }
    public string SampleThinEventsTopic { get; set; }
    public string Sample2BusinessEventsTopic { get; set; }
    public string Sample2FullRecordsTopic { get; set; }
    public string Sample2ThinEventsTopic { get; set; }
    public string GroupId { get; set; }
}
