using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECK1.Integration.Plugin.Abstractions;

public interface IIntergationPlugin<T>
{
    Task PushAsync(T message);
}

public interface IIntergationPluginLoader
{
    void Setup(IServiceCollection services, IConfiguration config, IntegrationConfig integrationConfig);
}

