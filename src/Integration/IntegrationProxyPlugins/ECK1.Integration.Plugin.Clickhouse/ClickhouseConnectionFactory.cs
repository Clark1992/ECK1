using ClickHouse.Client.ADO;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ECK1.Integration.Plugin.Clickhouse;

public interface IClickhouseConnectionFactory
{
    ValueTask<ClickHouseConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken);
}

public sealed class ClickhouseConnectionFactory : IClickhouseConnectionFactory
{
    private readonly ClickhouseConfig config;

    public ClickhouseConnectionFactory(ILogger<ClickhouseConnectionFactory> logger, ClickhouseConfig config)
    {
        this.config = config;

        logger.LogInformation("ClickhouseConfig: {config}", JsonSerializer.Serialize(config));
    }

    public async ValueTask<ClickHouseConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new ClickHouseConnection(config.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
