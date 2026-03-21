using ECK1.Orleans.Grains;
using Microsoft.Extensions.DependencyInjection;

namespace ECK1.Orleans;

public class GrainBuilder<TInput>(IServiceCollection services) where TInput : class
{
    private Func<TInput, string> keySelector;

    public GrainBuilder<TInput> WithGrainKey(Func<TInput, string> keySelector)
    {
        this.keySelector = keySelector;
        return this;
    }

    public StatefulGrainBuilder<TInput, TState, TResult> AsStateful<TState, TResult>()
        => new(services, keySelector);

    public StatefulGrainBuilder<TInput, TState> AsStateful<TState>()
        => new(services, keySelector);
}

public class StatefulGrainBuilder<TInput, TState, TResult>(
    IServiceCollection services,
    Func<TInput, string> keySelector) where TInput : class
{
    public GrainRouterConfigurator<TInput, NullGrainMetadata> HandledBy<THandler>()
        where THandler : class, IStatefulGrainHandler<TInput, TState, TResult>
        => HandledBy<THandler, NullGrainMetadata>();

    public GrainRouterConfigurator<TInput, TMetadata> HandledBy<THandler, TMetadata>()
        where THandler : class, IStatefulGrainHandler<TInput, TState, TResult>
        where TMetadata : IGrainMetadata
    {
        services.AddSingleton<IStatefulGrainHandler<TInput, TState, TResult>, THandler>();

        if (keySelector is not null)
        {
            services.AddSingleton<IGrainRouter<TInput, TMetadata, TResult>>(sp =>
            {
                var router = new GrainRouter<TInput, TMetadata, TState, TResult>(
                    sp.GetRequiredService<IClusterClient>());
                router.WithGrainKey(keySelector);
                return router;
            });
        }
        else
        {
            services.AddSingleton<IGrainRouter<TInput, TMetadata, TResult>>(sp =>
                new GrainRouter<TInput, TMetadata, TState, TResult>(
                    sp.GetRequiredService<IClusterClient>()));
        }

        return new GrainRouterConfigurator<TInput, TMetadata>(services);
    }
}

public class StatefulGrainBuilder<TInput, TState>(
    IServiceCollection services,
    Func<TInput, string> keySelector) where TInput : class
{
    public GrainRouterConfigurator<TInput, NullGrainMetadata> HandledBy<THandler>()
        where THandler : class, IStatefulGrainHandler<TInput, TState>
        => HandledBy<THandler, NullGrainMetadata>();

    public GrainRouterConfigurator<TInput, TMetadata> HandledBy<THandler, TMetadata>()
        where THandler : class, IStatefulGrainHandler<TInput, TState>
        where TMetadata : IGrainMetadata
    {
        services.AddSingleton<IStatefulGrainHandler<TInput, TState>, THandler>();

        if (keySelector is not null)
        {
            services.AddSingleton<IGrainRouter<TInput, TMetadata>>(sp =>
            {
                var router = new GrainRouter<TInput, TMetadata, TState>(
                    sp.GetRequiredService<IClusterClient>());
                router.WithGrainKey(keySelector);
                return router;
            });
        }
        else
        {
            services.AddSingleton<IGrainRouter<TInput, TMetadata>>(sp =>
                new GrainRouter<TInput, TMetadata, TState>(
                    sp.GetRequiredService<IClusterClient>()));
        }

        return new GrainRouterConfigurator<TInput, TMetadata>(services);
    }
}
