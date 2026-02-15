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

public class CacheServiceSettings
{
    public static string Section => "CacheService";

    public CacheEndpointSettings ShortTerm { get; set; } = new();
    public CacheEndpointSettings LongTerm { get; set; } = new();
    public CacheRetrySettings Retry { get; set; } = new();
    public int StaleEventThresholdMinutes { get; set; }
}

public class CacheEndpointSettings
{
    public string Url { get; set; }
}

public class CacheRetrySettings
{
    public int MaxAttempts { get; set; } = 3;
    public int DelayMs { get; set; } = 200;
}
