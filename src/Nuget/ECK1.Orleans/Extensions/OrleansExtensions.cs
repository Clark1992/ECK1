using ECK1.Orleans.Grains;
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
                    .AddActivityPropagation()
                    .UseLocalhostClustering()
                    .AddMemoryGrainStorage("RedisStore");
            }
            else if (hostingMode.Equals("k8s", StringComparison.OrdinalIgnoreCase))
            {
                siloBuilder
                    .AddActivityPropagation()
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

    public static IServiceCollection SetupOrleansDefaults(this IServiceCollection services) => services
            .AddSingleton(typeof(IMetadataStorage<>), typeof(NullMetadataStorage<>))
            .AddSingleton(typeof(IDupChecker<,>), typeof(DefaultDupChecker<,>))
            .AddSingleton(typeof(IMetadataUpdater<,>), typeof(DefaultMetadataUpdater<,>))
            .AddSingleton(typeof(IFaultedStateReset<>), typeof(DefaultFaultedStateReset<>));

    public static GrainBuilder<TInput> AddGrain<TInput>(this IServiceCollection services) where TInput : class
        => new(services);
}
