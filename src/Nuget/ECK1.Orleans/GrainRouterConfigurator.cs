using Microsoft.Extensions.DependencyInjection;
using System.Reflection.Metadata;

namespace ECK1.Orleans;

public interface IGrainRouterConfigurator<TEntity, TMetadata>
{
    IGrainRouterConfigurator<TEntity, TMetadata> AddDupChecker<TDupCheker>()
        where TDupCheker : class, IDupChecker<TEntity, TMetadata>;

    IGrainRouterConfigurator<TEntity, TMetadata> AddMetadataUpdater<TMetadataUpdater>()
        where TMetadataUpdater : class, IMetadataUpdater<TEntity, TMetadata>;

    IGrainRouterConfigurator<TEntity, TMetadata> AddFaultedStateReset<TFaultedStateReset>()
        where TFaultedStateReset : class, IFaultedStateReset<TEntity>;
}

public class GrainRouterConfigurator<TEntity, TMetadata> : IGrainRouterConfigurator<TEntity, TMetadata>
{
    private readonly IServiceCollection services;

    public GrainRouterConfigurator(IServiceCollection services)
    {
        this.services = services;
    }

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
}
