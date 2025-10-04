namespace ECK1.Orleans;

public class Ignore { }

public interface IDupChecker<TCriteria, TMetadata>
{
    bool IsProcessed(TCriteria actual, TMetadata persisted);
}

//public class SbDupChecker : IDupChecker<Guid, SBGrainMetadata>
//{
//    public bool IsProcessed(Guid actual, SBGrainMetadata persisted) => persisted.LastMessageIds.Contains(actual);
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

public interface IGenericStatelessGrain<TEntity, TMessageId, TMetadata> : IGrainWithStringKey
    where TEntity : class
{
    Task Process(TEntity e, TMessageId messageId, CancellationToken ct);
}

public interface IGenericStatefulGrain<TEntity, TMessageId, TMetadata, TState> : IGrainWithStringKey
    where TEntity : class
{
    Task Process(TEntity e, TMessageId messageId, CancellationToken ct);
}

public class GenericStatelessGrain<TEntity, TMessageId, TMetadata> : Grain, IGenericStatelessGrain<TEntity, TMessageId, TMetadata>
    where TEntity : class
{
    private readonly IPersistentState<TMetadata> metadata;
    private readonly IStatelessGrainHandler<TEntity> handler;
    private readonly IDupChecker<TMessageId, TMetadata> dupChecker;

    private bool isFaulted = false;

    public GenericStatelessGrain(
        [PersistentState("entityView", "RedisStore")] IPersistentState<TMetadata> metadata,
        IStatelessGrainHandler<TEntity> handler,
        IDupChecker<TMessageId, TMetadata> dupChecker)
    {
        this.metadata = metadata;
        this.handler = handler;
        this.dupChecker = dupChecker;
    }

    public async Task Process(TEntity e, TMessageId messageId, CancellationToken ct)
    {
        if (dupChecker.IsProcessed(messageId, metadata.State))
        {
            // Possible dup
            return;
        }

        try
        { 
            await handler.Handle(e, ct);
        }
        catch(Exception ex)
        {
            // TODO LOG
            isFaulted = true;
        }

        await metadata.WriteStateAsync();
    }
}

public abstract class GenericStatefulGrain<TEntity, TMessageId, TMetadata, TState> : 
    Grain, IGenericStatefulGrain<TEntity, TMessageId, TMetadata, TState>
    where TEntity : class
{
    private TState inMemoryState;
    private readonly IDupChecker<TMessageId, TMetadata> dupChecker;
    private readonly IStatefulGrainHandler<TEntity, TState> handler;
    private readonly IPersistentState<TMetadata> metadata;

    private bool isFaulted = false;

    protected IPersistentState<TMetadata> Metadata => metadata;

    public GenericStatefulGrain(
        [PersistentState("entityView", "RedisStore")] IPersistentState<TMetadata> metadata,
        IStatefulGrainHandler<TEntity, TState> handler,
        IDupChecker<TMessageId, TMetadata> dupChecker)
    {
        this.metadata = metadata;
        this.handler = handler;
        this.dupChecker = dupChecker;
    }

    public async Task Process(TEntity e, TMessageId messageId, CancellationToken ct)
    {
        //if (state.State is null)
        //    await state.ReadStateAsync();

        if (dupChecker.IsProcessed(messageId, Metadata.State))
        {
            // Possible dup
            return;
        }

        try
        {
            inMemoryState = await handler.Handle(e, inMemoryState,ct);
        } 
        catch(Exception ex)
        {
            // TODO LOG
            isFaulted = true;
        }

        MetadataUpdater(messageId);

        await Metadata.WriteStateAsync();
    }

    protected virtual void MetadataUpdater(TMessageId messageId)
    {
    }
}

public interface IGenericStatefulGrainRouter<TEntity, TMessageId, TMetadata, TState> where TEntity : class
{
}
