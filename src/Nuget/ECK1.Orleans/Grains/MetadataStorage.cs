namespace ECK1.Orleans.Grains;

public interface IGrainMetadata
{
    bool IsFaulted { get; set; }
}

public class GrainMetadata : IGrainMetadata
{
    public bool IsFaulted { get; set; }
}

public class NullGrainMetadata : IGrainMetadata
{
    public bool IsFaulted { get => false; set { } }
}

public interface IMetadataStorage<TMetadata> where TMetadata : IGrainMetadata
{
    TMetadata Metadata { get; }

    Task WriteStateAsync(CancellationToken ct);
}

public class RedisMetadataStorage<TMetadata>(
    [PersistentState("entityView", "RedisStore")] IPersistentState<TMetadata> grainMetadata) : IMetadataStorage<TMetadata> 
    where TMetadata : IGrainMetadata
{
    public TMetadata Metadata => grainMetadata.State;
    public Task WriteStateAsync(CancellationToken ct) =>
        grainMetadata.WriteStateAsync(ct);
}

public class NullMetadataStorage<TMetadata> : IMetadataStorage<TMetadata>
    where TMetadata : IGrainMetadata, new()
{
    public TMetadata Metadata { get; } = new();
    public Task WriteStateAsync(CancellationToken _) => Task.CompletedTask;
}