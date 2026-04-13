namespace ECK1.VersionTracker;

public class KafkaSettings
{
    public static string Section => "Kafka";
    public string BootstrapServers { get; set; }
    public string SchemaRegistryUrl { get; set; }
    public string User { get; set; }
    public string Secret { get; set; }
}

public class MongoDbSettings
{
    public static string Section => "MongoDb";
    public string ConnectionString { get; set; }
    public string DatabaseName { get; set; }
}
