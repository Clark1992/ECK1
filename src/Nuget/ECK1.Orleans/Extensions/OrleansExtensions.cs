using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using ECK1.Orleans.Kafka;

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
                    .AddRedisGrainStorage("RedisStore", options =>
                    {
                        options.ConfigurationOptions = ConfigurationOptions.Parse(redisConnection);
                    });
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

    public static IServiceCollection AddKafkaGrainRouter<TEntity, TState, TGrainHandler>(
        this IServiceCollection services,
        Func<TEntity, string> grainKeySelector)
        where TEntity : class
        where TGrainHandler : class, IStatefulGrainHandler<TEntity, TState>
    {
        services.AddSingleton<IDupChecker<long, KafkaGrainMetadata>, KafkaDupChecker>();
        services.AddSingleton<IStatefulGrainHandler<TEntity, TState>, TGrainHandler>();

        services.AddSingleton<IKafkaGrainRouter<TEntity>>(sp =>
        {
            var router = new KafkaGrainRouter<TEntity, TState>(sp.GetRequiredService<IClusterClient>());

            router.WithGrainKey(grainKeySelector);

            return router;
        });

        return services;
    }
}
