using ECK1.Orleans;
using ECK1.Orleans.Kafka;

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
    IFaultedStateReset<TEntity> faultedStateReset)
    : StatefulGrain<TEntity, TMetadata, TState>(state, handler)
    where TEntity : class
    where TMetadata : KafkaGrainMetadata
{
    public override async Task Process(TEntity e, CancellationToken ct)
    {
        if (dupChecker.IsMessageProcessed(e, Metadata.State))
        {
            // Possible dup
            return;
        }

        if (metadata.State.IsFaulted || !faultedStateReset.ShouldReset(e))
        {
            return;
        }

        try
        {
            await base.Process(e, ct);
        }
        catch (Exception ex)
        {
            // TODO LOG
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

    public Task RouteToGrain(TEntity evt, CancellationToken ct)
    {
        var grain = clusterClient.GetGrain<IStatefulGrain<TEntity, TMetadata, TState>>(keySelector(evt));

        return grain.Process(evt, ct);
    }

    public void WithGrainKey(Func<TEntity, string> selector) => keySelector = selector;
}
