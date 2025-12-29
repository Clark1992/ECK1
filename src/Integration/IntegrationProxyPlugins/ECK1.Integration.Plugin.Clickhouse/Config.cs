using Microsoft.Extensions.Configuration;

namespace ECK1.Integration.Plugin.Clickhouse;

public sealed class ClickhousePluginConfig
{
    public string Table { get; init; }

    public IConfigurationSection Mappings { get; init; }

}

public class ClickhousePayloadMapping
{
    public string Format { get; set; } = "json";

    public IConfigurationSection Fields { get; set; }
}

public sealed class ClickhouseConfig
{
    public string ConnectionString { get; set; }
}
