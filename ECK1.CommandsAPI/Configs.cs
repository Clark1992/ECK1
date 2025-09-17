namespace ECK1.CommandsAPI;

public class EventsStoreConfig
{
    public int SnapshotInterval { get; set; }
}

public class KafkaSettings
{
    public static string Section => "Kafka";
    public string BootstrapServers { get; set; }
    public string SchemaRegistryUrl { get; set; }
    public string User { get; set; }
    public string Secret { get; set; }
    public string SampleEventsTopic { get; set; }
}
