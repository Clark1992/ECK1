using Microsoft.Extensions.DependencyInjection;

namespace ECK1.CommonUtils.JobQueue;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddQueueProcessing(
        this IServiceCollection services,
        Action<IQueueRunnerConfig> configAction = null)
    {
        services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
        services.AddHostedService<QueuedBackgroundService>();

        if (configAction is not null)
        {
            var config = new QueueRunnerConfig(services);
            configAction.Invoke(config);
        }

        return services;
    }
}

public interface IQueueRunnerConfig
{
    IQueueRunnerConfig AddRunner(Type runnerInterfaceType, Type runnerImplementationType);

    IQueueRunnerConfig AddRunner<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService;
}

public class QueueRunnerConfig(IServiceCollection services) : IQueueRunnerConfig
{
    public IQueueRunnerConfig AddRunner(Type runnerInterfaceType, Type runnerImplementationType)
    {
        services.AddScoped(runnerInterfaceType, runnerImplementationType);
        return this;
    }

    public IQueueRunnerConfig AddRunner<TService, TImplementation>()
        where TService: class
        where TImplementation : class, TService
    {
        services.AddScoped<TService, TImplementation>();
        return this;
    }
}
