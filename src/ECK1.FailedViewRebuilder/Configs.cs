namespace ECK1.FailedViewRebuilder;

public class KafkaSettings
{
    public static string Section => "Kafka";
    public string BootstrapServers { get; set; }
    public string SchemaRegistryUrl { get; set; }
    public string User { get; set; }
    public string Secret { get; set; }
    public string FailureEventsTopic { get; set; }
    public string GroupId { get; set; }
}

public class FailureHandlingConfig : Dictionary<string, FailureHandlingConfigEntry>
{
    public static string Section => nameof(FailureHandlingConfig);
}

public class FailureHandlingConfigEntry
{
    public string RebuildRequestTopic { get; set; }
}