using ECK1.Orleans.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace ECK1.Orleans.Extensions;

public static class OrleansExtensions
{
    public static IHostBuilder SetupOrleansHosting(
        this IHostBuilder builder)
    {
        builder.UseOrleans((ctx, siloBuilder) =>
        {
            var redisConnection = ctx.Configuration["REDIS_CONNECTION"];
            var hostingMode = ctx.Configuration["ORLEANS_HOSTING"];

            // TODO: review
            if (hostingMode.Equals("local", StringComparison.OrdinalIgnoreCase))
            {
                siloBuilder
                    .UseLocalhostClustering()
                    .AddMemoryGrainStorage("RedisStore");
            }
            else if (hostingMode.Equals("k8s", StringComparison.OrdinalIgnoreCase))
            {
                siloBuilder
                    .UseKubernetesHosting()
                    .UseRedisClustering(redisConnection)
                    .AddRedisGrainStorage("RedisStore", options =>
                    {
                        options.ConfigurationOptions = ConfigurationOptions.Parse(redisConnection);
                    });
            }
        });

        return builder;
    }

    public static GrainRouterConfigurator<TEntity, TMetadata> AddKafkaGrainRouter<TEntity, TMetadata, TState, TGrainHandler>(
        this IServiceCollection services,
        Func<TEntity, string> grainKeySelector)
        where TEntity : class
        where TGrainHandler : class, IStatefulGrainHandler<TEntity, TState>
        where TMetadata : KafkaGrainMetadata
    {
        services.AddSingleton<IStatefulGrainHandler<TEntity, TState>, TGrainHandler>();

        services.AddSingleton<IKafkaGrainRouter<TEntity, TMetadata>>(sp =>
        {
            var router = new KafkaGrainRouter<TEntity, TMetadata, TState>(sp.GetRequiredService<IClusterClient>());

            router.WithGrainKey(grainKeySelector);

            return router;
        });

        return new GrainRouterConfigurator<TEntity, TMetadata>(services);
    }
}
