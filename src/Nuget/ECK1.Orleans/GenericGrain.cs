namespace ECK1.Orleans;

public class Ignore { }

public interface IDupChecker<TEntity, TMetadata>
{
    bool IsMessageProcessed(TEntity entity, TMetadata persisted) => false;
}

public class DefaultDupChecker<TEntity, TMetadata>: IDupChecker<TEntity, TMetadata>
{
}

public interface IMetadataUpdater<TEntity, TMetadata>
{
    void Update(TEntity entity, TMetadata persisted) { }
}

public class DefaulMetadataUpdater<TEntity, TMetadata> : IMetadataUpdater<TEntity, TMetadata>
{
}

public interface IFaultedStateReset<TEntity>
{
    bool ShouldReset(TEntity entity) => false;
}

public class DefaulFaultedStateReset<TEntity> : IFaultedStateReset<TEntity>
{
}

//public class SbDupChecker : IDupChecker<Guid, SBGrainMetadata>
//{
//    public bool IsProcessed(Guid actual, SBGrainMetadata persisted) => persisted.LasTDupCriterias.Contains(actual);
//}

//public class SBGrainMetadata
//{
//    public List<Guid> LastMessageIds { get; set; }
//}

public interface IStatefulGrainHandler<TEntity, TState>
{
    Task<TState> Handle(TEntity entity, TState state, CancellationToken ct);
}

public interface IStatelessGrainHandler<TEntity>
{
    Task Handle(TEntity entity, CancellationToken ct);
}

public interface IStatelessGrain<TEntity, TMetadata> : IGrainWithStringKey
    where TEntity : class
{
    Task Process(TEntity e, CancellationToken ct);
}

public interface IStatefulGrain<TEntity, TMetadata, TState> : IGrainWithStringKey
    where TEntity : class
{
    Task Process(TEntity e, CancellationToken ct);
}

public abstract class StatelessGrain<TEntity, TMetadata> : Grain, IStatelessGrain<TEntity, TMetadata>
    where TEntity : class
{
    private readonly IPersistentState<TMetadata> metadata;
    private readonly IStatelessGrainHandler<TEntity> handler;
    private readonly IDupChecker<TEntity, TMetadata> dupChecker;

    public StatelessGrain(
        [PersistentState("entityView", "RedisStore")] IPersistentState<TMetadata> metadata,
        IStatelessGrainHandler<TEntity> handler,
        IDupChecker<TEntity, TMetadata> dupChecker)
    {
        this.metadata = metadata;
        this.handler = handler;
        this.dupChecker = dupChecker;
    }

    public virtual async Task Process(TEntity e, CancellationToken ct)
    {
        if (dupChecker.IsMessageProcessed(e, metadata.State))
        {
            // Possible dup
            return;
        }

        await handler.Handle(e, ct);

        await metadata.WriteStateAsync(default);
    }
}

public abstract class StatefulGrain<TEntity, TMetadata, TState> : 
    Grain, IStatefulGrain<TEntity, TMetadata, TState>
    where TEntity : class
{
    private TState inMemoryState;
    private readonly IStatefulGrainHandler<TEntity, TState> handler;
    protected readonly IPersistentState<TMetadata> metadata;

    protected IPersistentState<TMetadata> Metadata => metadata;

    public StatefulGrain(
        [PersistentState("entityView", "RedisStore")] IPersistentState<TMetadata> metadata,
        IStatefulGrainHandler<TEntity, TState> handler)
    {
        this.metadata = metadata;
        this.handler = handler;
    }

    public virtual async Task Process(TEntity entity, CancellationToken ct)
    {
        inMemoryState = await handler.Handle(entity, inMemoryState,ct);

        MetadataUpdater(entity, metadata.State);

        await Metadata.WriteStateAsync(default);
    }

    protected virtual void MetadataUpdater(TEntity e, TMetadata meta)
    {
    }
}

public interface IGenericStatefulGrainRouter<TEntity, TMetadata, TState> where TEntity : class
{
}
