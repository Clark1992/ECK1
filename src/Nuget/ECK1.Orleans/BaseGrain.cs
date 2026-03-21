using ECK1.Orleans.Grains;
using Microsoft.Extensions.Logging;

namespace ECK1.Orleans;

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

public class DefaultMetadataUpdater<TEntity, TMetadata> : IMetadataUpdater<TEntity, TMetadata>
{
}

public interface IFaultedStateReset<TEntity>
{
    bool ShouldReset(TEntity entity) => false;
}

public class DefaultFaultedStateReset<TEntity> : IFaultedStateReset<TEntity>
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

public interface IStatefulGrainHandler<TInput, TState, TResult>
{
    Task<(TResult, TState)> Handle(TInput entity, TState state, CancellationToken ct);
}

public interface IStatefulGrainHandler<TInput, TState>
{
    Task<TState> Handle(TInput entity, TState state, CancellationToken ct);
}

public interface IStatelessGrainHandler<TInput, TResult>
{
    Task<TResult> Handle(TInput input, CancellationToken ct);
}

public interface IStatelessGrain<TInput, TMetadata, TResult> : IGrainWithStringKey
    where TInput : class
{
    Task<TResult> Process(TInput e, CancellationToken ct);
}

public interface IStatefulGrain<TInput, TMetadata, TState, TResult> : IGrainWithStringKey
    where TInput : class
{
    Task<TResult> Process(TInput e, CancellationToken ct);
}

public interface IStatefulGrain<TInput, TMetadata, TState> : IGrainWithStringKey
    where TInput : class
{
    Task Process(TInput e, CancellationToken ct);
}

public abstract class StatelessGrain<TInput, TMetadata, TResult>(
    IMetadataStorage<TMetadata> metadataStorage,
    IStatelessGrainHandler<TInput, TResult> handler,
    IDupChecker<TInput, TMetadata> dupChecker,
    ILogger<StatelessGrain<TInput, TMetadata, TResult>> logger) : Grain, IStatelessGrain<TInput, TMetadata, TResult>
    where TInput : class
    where TMetadata : IGrainMetadata
{
    public virtual async Task<TResult> Process(TInput e, CancellationToken ct)
    {
        if (dupChecker.IsMessageProcessed(e, metadataStorage.Metadata))
        {
            // Possible dup
            logger.LogWarning("Possible dup detected. Skipping.");
            return default;
        }

        var result = await handler.Handle(e, ct);

        await metadataStorage.WriteStateAsync(default);

        return result;
    }
}

public abstract class StatefulGrain<TInput, TMetadata, TState, TResult> : 
    Grain, IStatefulGrain<TInput, TMetadata, TState, TResult>
    where TInput : class
    where TMetadata : IGrainMetadata
{
    private TState inMemoryState;
    private readonly IStatefulGrainHandler<TInput, TState, TResult> handler;
    private readonly IMetadataStorage<TMetadata> metadataStorage;

    protected IMetadataStorage<TMetadata> MetadataStorage => metadataStorage;

    public StatefulGrain(
        IMetadataStorage<TMetadata> metadataStorage,
        IStatefulGrainHandler<TInput, TState, TResult> handler)
    {
        this.metadataStorage = metadataStorage;
        this.handler = handler;
    }

    public virtual async Task<TResult> Process(TInput value, CancellationToken ct)
    {
        var (result, state) = await handler.Handle(value, inMemoryState, ct);
        inMemoryState = state ?? inMemoryState;

        MetadataUpdater(value, MetadataStorage.Metadata);

        await MetadataStorage.WriteStateAsync(default);

        return result;
    }

    protected virtual void MetadataUpdater(TInput e, TMetadata meta)
    {
    }
}
