using System.Text.Json;
using System.Text.Json.Nodes;

namespace ECK1.Integration.Proxy;

public class KafkaSettings
{
    public static string Section => "Kafka";
    public string BootstrapServers { get; set; }
    public string SchemaRegistryUrl { get; set; }
    public string User { get; set; }
    public string Secret { get; set; }
    public string SampleThinEventsTopic { get; set; }
    public string CacheProgressTopic { get; set; }
    public string GroupIdPrefix { get; set; }
}
