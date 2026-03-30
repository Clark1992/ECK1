using ECK1.Integration.Plugin.Abstractions;
using ECK1.Integration.Plugin.Abstractions.ProjectionCompiler;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ClickHouse.Client.Copy;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;
using ECK1.Integration.Config;
using OpenTelemetry.Trace;
using Generated = ECK1.IntegrationContracts.Kafka.IntegrationRecords.Generated;

namespace ECK1.Integration.Plugin.Clickhouse;

public class ClickhousePluginLoader : IIntergationPluginLoader
{
    public void Setup(IServiceCollection services, IConfiguration config, IntegrationConfig integrationConfig)
    {
        services.AddSingleton(integrationConfig);

        var chConfig = config.GetSection(nameof(ClickhouseConfig)).Get<ClickhouseConfig>();
        services.AddSingleton(chConfig);
        
        services.AddSingleton(typeof(IIntergationPlugin<,>), typeof(ClickhouseWriter<,>));

        services.AddSingleton<IReconciliationPlugin, ClickhouseReconciliationPlugin>();
        services.AddSingleton<IClickhouseConnectionFactory, ClickhouseConnectionFactory>();
    }

    public void SetupTelemetry(TracerProviderBuilder tracing)
    {
        tracing.AddClickHouse();
    }
}

public sealed class ClickhouseWriter<TEvent, TRecord>: IIntergationPlugin<TEvent, TRecord>, IAsyncDisposable
    where TEvent : Generated.ThinEvent
{
    private readonly ILogger<ClickhouseWriter<TEvent, TRecord>> logger;

    private readonly IClickhouseConnectionFactory connectionFactory;
    private readonly ClickhousePluginConfig pluginConfig;

    private readonly ExecutionPlan<TEvent, TRecord> plan;
    private readonly string[] columnNames;

    private ConcurrentQueue<object[]> buffer = new();
    private readonly SemaphoreSlim flushLock = new(1, 1);
    private readonly CancellationTokenSource stopCts = new();
    private readonly Task flushLoop;

    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromMilliseconds(1000);

    public ClickhouseWriter(
        ILogger<ClickhouseWriter<TEvent, TRecord>> logger,
        IntegrationConfig integrationConfig,
        IClickhouseConnectionFactory connectionFactory)
    {
        string messageType = typeof(TRecord).FullName;
        this.pluginConfig = integrationConfig.TryGetValue(
                messageType, out var entry) ?
            entry.PluginConfig.Get<ClickhousePluginConfig>() :
            throw new InvalidOperationException($"Missing plugin config for {messageType}");

        //this.config = options.Value;
        this.connectionFactory = connectionFactory;
        this.logger = logger;
        this.logger.LogInformation("Clickhouse: loaded");
        this.logger.LogInformation("PluginConfig: {config}", JsonSerializer.Serialize(this.pluginConfig));

        if (string.IsNullOrEmpty(this.pluginConfig.Table))
            throw new InvalidOperationException($"Clickhouse:Table is missing for {messageType}");
        if (!this.pluginConfig.Mappings.Exists())
            throw new InvalidOperationException($"Clickhouse:Mappings is missing for {messageType}");

        this.plan = ProjectionPlanCompiler.Compile<TEvent, TRecord>(pluginConfig.Mappings);
        this.columnNames = plan.ColumnNames;


        flushLoop = Task.Run(() => FlushLoopAsync(stopCts.Token), CancellationToken.None);
    }

    public async Task PushAsync(TEvent @event, TRecord message)
    {
        try
        {
            var row = plan.ColumnValues(@event, message);
            buffer.Enqueue(row);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during calling CH");
        }

        await Task.CompletedTask;
    }

    private async Task FlushLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(FlushInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await FlushOnceAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException ex)
        {
            logger.LogError(ex, "OperationCanceledException");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Clickhouse flush loop crashed");
        }
    }

    private async Task FlushOnceAsync(CancellationToken cancellationToken)
    {
        if (buffer.IsEmpty)
            return;

        if (!await flushLock.WaitAsync(0, cancellationToken))
            return;

        ConcurrentQueue<object[]> toFlush;
        try
        {
            if (buffer.IsEmpty)
                return;

            toFlush = Interlocked.Exchange(ref buffer, new ConcurrentQueue<object[]>());
        }
        finally
        {
            flushLock.Release();
        }

        var rows = new List<object[]>(capacity: 256);
        while (toFlush.TryDequeue(out var row))
            rows.Add(row);

        if (rows.Count == 0)
            return;

        try
        {
            await using var conn = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

            var bulk = new ClickHouseBulkCopy(conn)
            {
                DestinationTableName = pluginConfig.Table,
                ColumnNames = columnNames
            };
            await bulk.InitAsync();

            await bulk.WriteToServerAsync(rows, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Clickhouse bulk insert failed; re-queueing {count} rows", rows.Count);
            foreach (var row in rows)
                buffer.Enqueue(row);
        }
    }

    public async ValueTask DisposeAsync()
    {
        stopCts.Cancel();
        try
        {
            await flushLoop;
        }
        catch
        {
        }

        stopCts.Dispose();
        flushLock.Dispose();
    }
}

public class ClickhouseReconciliationPlugin(
    IClickhouseConnectionFactory connectionFactory,
    ILogger<ClickhouseReconciliationPlugin> logger) : IReconciliationPlugin
{
    public async Task<ReconciliationCheckResult> CheckAsync(Guid entityId, string entityType, int expectedVersion, CancellationToken ct)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT max(entity_version) AS max_ver, count() AS cnt
            FROM integration_events_raw FINAL
            WHERE entity_id = {entityId:UUID} AND entity_type = {entityType:String}
            """;
        cmd.Parameters.Add(new ClickHouse.Client.ADO.Parameters.ClickHouseDbParameter
        {
            ParameterName = "entityId",
            Value = entityId
        });
        cmd.Parameters.Add(new ClickHouse.Client.ADO.Parameters.ClickHouseDbParameter
        {
            ParameterName = "entityType",
            Value = entityType
        });

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct))
        {
            logger.LogWarning("CH reconciliation: no rows for entity {EntityId}", entityId);
            return ReconciliationCheckResult.NeedsFullRebuild;
        }

        var maxVer = reader.GetInt32(0);
        var cnt = reader.GetInt64(1);

        if (cnt == 0)
        {
            logger.LogWarning("CH reconciliation: entity {EntityId} not found", entityId);
            return ReconciliationCheckResult.NeedsFullRebuild;
        }

        if (maxVer < expectedVersion)
        {
            logger.LogWarning("CH reconciliation: entity {EntityId} version mismatch. Expected {Expected}, got {Actual}",
                entityId, expectedVersion, maxVer);
            return ReconciliationCheckResult.NeedsFullRebuild;
        }

        if (cnt < maxVer)
        {
            logger.LogWarning("CH reconciliation: entity {EntityId} has gaps. Expected {Expected} rows, got {Actual}",
                entityId, maxVer, cnt);
            return ReconciliationCheckResult.NeedsFullRebuild;
        }

        return ReconciliationCheckResult.Ok;
    }
}