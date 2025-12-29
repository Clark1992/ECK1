using ECK1.Integration.Plugin.Abstractions;
using ECK1.Integration.Plugin.Abstractions.ProjectionCompiler;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ClickHouse.Client.Copy;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace ECK1.Integration.Plugin.Clickhouse;

public class ClickhousePluginLoader : IIntergationPluginLoader
{
    public void Setup(IServiceCollection services, IConfiguration config, IntegrationConfig integrationConfig)
    {
        services.AddSingleton(integrationConfig);

        var chConfig = config.GetSection(nameof(ClickhouseConfig)).Get<ClickhouseConfig>();
        services.AddSingleton(chConfig);
        
        services.AddSingleton(typeof(IIntergationPlugin<,>), typeof(ClickhouseWriter<,>));

        services.AddSingleton<IClickhouseConnectionFactory, ClickhouseConnectionFactory>();
    }
}

public sealed class ClickhouseWriter<TEvent, TRecord>: IIntergationPlugin<TEvent, TRecord>, IAsyncDisposable
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