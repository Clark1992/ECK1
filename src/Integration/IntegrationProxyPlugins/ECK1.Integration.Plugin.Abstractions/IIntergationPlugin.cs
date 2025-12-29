using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECK1.Integration.Plugin.Abstractions;

public interface IIntergationPlugin<TEvent, TMessage>
{
    Task PushAsync(TEvent @event, TMessage message);
}

public interface IIntergationPluginLoader
{
    void Setup(IServiceCollection services, IConfiguration config, IntegrationConfig integrationConfig);
}

