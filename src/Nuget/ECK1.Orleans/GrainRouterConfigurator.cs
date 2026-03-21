using ECK1.Orleans.Grains;
using Microsoft.Extensions.DependencyInjection;

namespace ECK1.Orleans;

public interface IGrainRouterConfigurator<TEntity, TMetadata>
    where TMetadata : IGrainMetadata
{
    IGrainRouterConfigurator<TEntity, TMetadata> AddDupChecker<TDupCheker>()
        where TDupCheker : class, IDupChecker<TEntity, TMetadata>;

    IGrainRouterConfigurator<TEntity, TMetadata> AddMetadataUpdater<TMetadataUpdater>()
        where TMetadataUpdater : class, IMetadataUpdater<TEntity, TMetadata>;

    IGrainRouterConfigurator<TEntity, TMetadata> AddFaultedStateReset<TFaultedStateReset>()
        where TFaultedStateReset : class, IFaultedStateReset<TEntity>;

    IGrainRouterConfigurator<TEntity, TMetadata> UseMetadataStorage<TStorage>()
        where TStorage : class, IMetadataStorage<TMetadata>;
}

public class GrainRouterConfigurator<TEntity, TMetadata>(IServiceCollection services) : IGrainRouterConfigurator<TEntity, TMetadata>
    where TMetadata : IGrainMetadata
{
    public IGrainRouterConfigurator<TEntity, TMetadata> AddDupChecker<TDupCheker>()
        where TDupCheker : class, IDupChecker<TEntity, TMetadata>
    {
        services.AddSingleton<IDupChecker<TEntity, TMetadata>, TDupCheker>();
        return this;
    }

    public IGrainRouterConfigurator<TEntity, TMetadata> AddMetadataUpdater<TMetadataUpdater>()
        where TMetadataUpdater : class, IMetadataUpdater<TEntity, TMetadata>
    {
        services.AddSingleton<IMetadataUpdater<TEntity, TMetadata>, TMetadataUpdater>();
        return this;
    }

    public IGrainRouterConfigurator<TEntity, TMetadata> AddFaultedStateReset<TFaultedStateReset>()
        where TFaultedStateReset : class, IFaultedStateReset<TEntity>
    {
        services.AddSingleton<IFaultedStateReset<TEntity>, TFaultedStateReset>();
        return this;
    }

    public IGrainRouterConfigurator<TEntity, TMetadata> UseMetadataStorage<TStorage>()
        where TStorage : class, IMetadataStorage<TMetadata>
    {
        services.AddSingleton<IMetadataStorage<TMetadata>, TStorage>();
        return this;
    }
}
