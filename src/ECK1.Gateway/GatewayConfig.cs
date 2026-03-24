namespace ECK1.Gateway;

public class GatewayConfig
{
    public const string Section = "Gateway";

    public string Namespace { get; set; } = "default";
    public int RefreshIntervalSeconds { get; set; } = 30;
    public string SwaggerPathTemplate { get; set; } = "/swagger/v1/swagger.json";
    public string AsyncApiPath { get; set; } = "/.well-known/async-api.json";
    public List<StaticServiceEntry> StaticServices { get; set; } = [];
}

public class StaticServiceEntry
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool ExposeApi { get; set; }
    public bool ExposeAsyncApi { get; set; }
}

public class KafkaSettings
{
    public const string Section = "Kafka";

    public string BootstrapServers { get; set; } = string.Empty;
    public string SchemaRegistryUrl { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
}
