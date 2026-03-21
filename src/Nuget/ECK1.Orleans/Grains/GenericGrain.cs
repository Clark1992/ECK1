using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ECK1.Orleans.Grains;

public class GenericGrain<TInput, TMetadata, TState, TResult>(
    IMetadataStorage<TMetadata> metadataStorage,
    IStatefulGrainHandler<TInput, TState, TResult> handler,
    IDupChecker<TInput, TMetadata> dupChecker,
    IMetadataUpdater<TInput, TMetadata> metadataUpdater,
    IFaultedStateReset<TInput> faultedStateReset,
    ILogger<GenericGrain<TInput, TMetadata, TState, TResult>> logger)
    : StatefulGrain<TInput, TMetadata, TState, TResult>(metadataStorage, handler)
    where TInput : class
    where TMetadata : IGrainMetadata
{
    public override async Task<TResult> Process(TInput e, CancellationToken ct)
    {
        var traceId = Activity.Current?.TraceId.ToString();
        var spanId = Activity.Current?.SpanId.ToString();

        using var _ = string.IsNullOrWhiteSpace(traceId)
            ? null
            : logger.BeginScope(new Dictionary<string, object>
            {
                ["TraceId"] = traceId,
                ["SpanId"] = spanId,
                ["trace_id"] = traceId,
                ["span_id"] = spanId,
            });

        if (dupChecker.IsMessageProcessed(e, MetadataStorage.Metadata))
        {
            logger.LogInformation("Skipping dup: {input}", e);
            return default;
        }

        if (MetadataStorage.Metadata.IsFaulted && !faultedStateReset.ShouldReset(e))
        {
            logger.LogInformation("Shortcutting faulted: {input}", e);
            return default;
        }

        try
        {
            return await base.Process(e, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in kafka grain {Id}. Marking as faulted. Input: {Input}, Metadata: {Metadata}", 
                this.IdentityString,
                e,
                MetadataStorage.Metadata);
            await MarkFaulted();
            return default;
        }
    }

    protected virtual async Task MarkFaulted()
    {
        MetadataStorage.Metadata.IsFaulted = true;
        await MetadataStorage.WriteStateAsync(default);
    }

    protected override void MetadataUpdater(TInput e, TMetadata meta)
    {
        metadataUpdater.Update(e, meta);
    }
}

public interface IGrainRouter<TInput, TMetadata> where TInput : class
{
    Task RouteToGrain(TInput value, CancellationToken ct);
    
    void WithGrainKey(Func<TInput, string> selector);
}

public interface IGrainRouter<TInput, TMetadata, TResult> where TInput : class
{
    Task<TResult> RouteToGrain(TInput value, CancellationToken ct);

    void WithGrainKey(Func<TInput, string> selector);
}

public class GrainRouter<TInput, TMetadata, TState, TResult>(IClusterClient clusterClient) : IGrainRouter<TInput, TMetadata, TResult>
    where TInput : class
    where TMetadata : IGrainMetadata
{
    private Func<TInput, string> keySelector;

    public async Task<TResult> RouteToGrain(TInput value, CancellationToken ct)
    {
        var key = value is IGrainKeyResolver prefixed
            ? prefixed.ResolveGrainKey()
            : keySelector?.Invoke(value) ?? throw new InvalidOperationException("No grain key resolver configured");

        var grain = clusterClient.GetGrain<IStatefulGrain<TInput, TMetadata, TState, TResult>>(key);

        return await grain.Process(value, ct);
    }

    public void WithGrainKey(Func<TInput, string> selector) => keySelector = selector;
}

public class GrainRouter<TInput, TMetadata, TState>(IClusterClient clusterClient) : IGrainRouter<TInput, TMetadata>
    where TInput : class
    where TMetadata : IGrainMetadata
{
    private Func<TInput, string> keySelector;

    public async Task RouteToGrain(TInput value, CancellationToken ct)
    {
        var key = value is IGrainKeyResolver prefixed
            ? prefixed.ResolveGrainKey()
            : keySelector?.Invoke(value) ?? throw new InvalidOperationException("No grain key resolver configured");

        var grain = clusterClient.GetGrain<IStatefulGrain<TInput, TMetadata, TState>>(key);

        await grain.Process(value, ct);
    }

    public void WithGrainKey(Func<TInput, string> selector) => keySelector = selector;
}