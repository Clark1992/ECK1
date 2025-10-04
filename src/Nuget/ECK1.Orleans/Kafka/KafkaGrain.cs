namespace ECK1.Orleans.Kafka;


public class KafkaDupChecker : IDupChecker<long, KafkaGrainMetadata>
{
    public bool IsProcessed(long actual, KafkaGrainMetadata persisted) => actual < persisted.LastProcessedOffset;
}

public class KafkaGrainMetadata
{
    public long LastProcessedOffset { get; set; }
}

public class KafkaGrain<TEntity, _TIgnore1, _TIgnore2, TState>(
    [PersistentState("entityView", "RedisStore")] IPersistentState<KafkaGrainMetadata> state,
    IStatefulGrainHandler<TEntity, TState> handler,
    IDupChecker<long, KafkaGrainMetadata> dupChecker)
    : GenericStatefulGrain<TEntity, long, KafkaGrainMetadata, TState>(state, handler, dupChecker)
    where TEntity : class
{
    protected override void MetadataUpdater(long messageId)
    {
        // this.Metadata.State.LastProcessedOffset++;
        Metadata.State.LastProcessedOffset = messageId;
    }
}

public interface IKafkaGrainRouter<TEntity> where TEntity : class
{
    Task RouteToGrain(TEntity evt, long messageId, CancellationToken ct);
    void WithGrainKey(Func<TEntity, string> selector);
}

public class KafkaGrainRouter<TEntity, TState>(IClusterClient clusterClient) : IKafkaGrainRouter<TEntity>
    where TEntity : class
{
    private Func<TEntity, string> keySelector;

    public Task RouteToGrain(TEntity evt, long messageId, CancellationToken ct)
    {
        var grain = clusterClient.GetGrain<IGenericStatefulGrain<TEntity, long, KafkaGrainMetadata, TState>>(keySelector(evt));

        return grain.Process(evt, messageId, ct);
    }

    public void WithGrainKey(Func<TEntity, string> selector)
    {
        keySelector = selector;
    }
}
