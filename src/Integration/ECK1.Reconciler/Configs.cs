namespace ECK1.Reconciler;

public class KafkaSettings
{
    public static string Section => "Kafka";
    public string BootstrapServers { get; set; } = "";
    public string SchemaRegistryUrl { get; set; } = "";
    public string User { get; set; } = "";
    public string Secret { get; set; } = "";
    public string GroupId { get; set; } = "";
    public string ReconcileRequestsTopic { get; set; } = "";
    public string ReconcileResultsTopic { get; set; } = "";
}

public class ReconcilerSettings
{
    public static string Section => "Reconciler";
    public int ReconciliationCheckIntervalMinutes { get; set; } = 5;
    public int RebuildDispatchIntervalMinutes { get; set; } = 5;
    public int ReconcileBatchSize { get; set; } = 100;
    public int RebuildBatchSize { get; set; } = 100;
}
