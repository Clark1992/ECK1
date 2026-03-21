namespace ECK1.Orleans;

public interface IGrainKeyResolver
{
    string ResolveGrainKey();
}

public interface IGrainKeyResolver<TEntity> : IGrainKeyResolver
{
    string IGrainKeyResolver.ResolveGrainKey()
    {
        var prefix = typeof(TEntity).Name;
        var id = this is IValueId existing
            ? existing.ValueId
            : Guid.NewGuid().ToString();
        return $"{prefix}_{id}";
    }
}

public interface IValueId
{
    string ValueId { get; }
}

public interface IValueId<TId> : IValueId
{
    TId Id { get; }
    string IValueId.ValueId => Id?.ToString()!;
}
