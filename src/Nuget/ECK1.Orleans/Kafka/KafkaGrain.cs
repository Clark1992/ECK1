using ECK1.Orleans;
using ECK1.Orleans.Kafka;
using Microsoft.Extensions.Logging;
using Orleans.Runtime;
using System.Collections.Generic;
using System.Diagnostics;

namespace ECK1.Orleans.Kafka;

public class KafkaGrainMetadata
{
    public bool IsFaulted { get; set; }
}

public class KafkaGrain<TEntity, TMetadata, TState>(
    [PersistentState("entityView", "RedisStore")] IPersistentState<TMetadata> state,
    IStatefulGrainHandler<TEntity, TState> handler,
    IDupChecker<TEntity, TMetadata> dupChecker,
    IMetadataUpdater<TEntity, TMetadata> metadataUpdater,
    IFaultedStateReset<TEntity> faultedStateReset,
    ILogger<KafkaGrain<TEntity, TMetadata, TState>> logger)
    : StatefulGrain<TEntity, TMetadata, TState>(state, handler)
    where TEntity : class
    where TMetadata : KafkaGrainMetadata
{
    public override async Task Process(TEntity e, CancellationToken ct)
    {
        // Correlate Orleans-side logs with the upstream trace (e.g., Kafka consumer span).
        // Orleans calls may not always preserve Activity.Current across boundaries, so we also
        // carry TraceId/SpanId via RequestContext.
        var traceId = RequestContext.Get("TraceId") as string;
        var spanId = RequestContext.Get("SpanId") as string;

        if (string.IsNullOrWhiteSpace(traceId) && Activity.Current is not null)
        {
            traceId = Activity.Current.TraceId.ToString();
            spanId = Activity.Current.SpanId.ToString();
        }

        using var _ = string.IsNullOrWhiteSpace(traceId)
            ? null
            : logger.BeginScope(new Dictionary<string, object?>
            {
                ["TraceId"] = traceId,
                ["SpanId"] = spanId,
                ["trace_id"] = traceId,
                ["span_id"] = spanId,
            });

        if (dupChecker.IsMessageProcessed(e, Metadata.State))
        {
            logger.LogInformation("Skipping dup: {input}", e);
            return;
        }

        if (metadata.State.IsFaulted && !faultedStateReset.ShouldReset(e))
        {
            logger.LogInformation("Shortcutting faulted: {input}", e);
            return;
        }

        try
        {
            await base.Process(e, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in kafka grain {id}. Marking as faulted. Input: {input}, Metadata: {state}", 
                this.IdentityString,
                e,
                Metadata.State);
            await MarkFaulted();
        }
    }

    private async Task MarkFaulted()
    {
        metadata.State.IsFaulted = true;
        await Metadata.WriteStateAsync(default);
    }

    protected override void MetadataUpdater(TEntity e, TMetadata meta)
    {
        metadataUpdater.Update(e, meta);
    }
}

public interface IKafkaGrainRouter<TEntity, TMetadata> where TEntity : class
{
    Task RouteToGrain(TEntity evt, CancellationToken ct);
    
    void WithGrainKey(Func<TEntity, string> selector);
}

public class KafkaGrainRouter<TEntity, TMetadata, TState>(IClusterClient clusterClient) : IKafkaGrainRouter<TEntity, TMetadata>
    where TEntity : class
    where TMetadata : KafkaGrainMetadata
{
    private Func<TEntity, string> keySelector;

    public async Task RouteToGrain(TEntity evt, CancellationToken ct)
    {
        var grain = clusterClient.GetGrain<IStatefulGrain<TEntity, TMetadata, TState>>(keySelector(evt));

        var activity = Activity.Current;
        if (activity is null)
        {
            await grain.Process(evt, ct);
            return;
        }

        // Ensure the trace context is available inside the Orleans grain.
        // (Kafka consumer instrumentation creates Activity.Current around message handling.
        // Some Orleans execution paths may drop Activity.Current, so we duplicate it here.)
        var previousTraceId = RequestContext.Get("TraceId");
        var previousSpanId = RequestContext.Get("SpanId");

        try
        {
            RequestContext.Set("TraceId", activity.TraceId.ToString());
            RequestContext.Set("SpanId", activity.SpanId.ToString());

            await grain.Process(evt, ct);
        }
        finally
        {
            RequestContext.Set("TraceId", previousTraceId);
            RequestContext.Set("SpanId", previousSpanId);
        }
    }

    public void WithGrainKey(Func<TEntity, string> selector) => keySelector = selector;
}
