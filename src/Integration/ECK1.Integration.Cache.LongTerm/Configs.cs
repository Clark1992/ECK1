namespace ECK1.Integration.Cache.LongTerm;

public class KafkaSettings
{
    public static string Section => "Kafka";
    public string BootstrapServers { get; set; }
    public string SchemaRegistryUrl { get; set; }
    public string User { get; set; }
    public string Secret { get; set; }
    public string CacheProgressTopic { get; set; }
    public string GroupId { get; set; } = Guid.NewGuid().ToString();
}

public class NatsSettings
{
    public static string Section => "Nats";

    public string Url { get; set; } = "nats://nats:4222";
    public int MaxHistory { get; set; } = 1;
}

public class CacheConfig
{
    public static string Section => "Cache";
}
