using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ECK1.Integration.Common;
using OpenTelemetry.Trace;

namespace ECK1.Integration.Plugin.Abstractions;

public interface IIntergationPlugin<TEvent, TMessage>
{
    Task PushAsync(TEvent @event, TMessage message);
}

public interface IIntergationPluginLoader
{
    void Setup(IServiceCollection services, IConfiguration config, IntegrationConfig integrationConfig);
    void SetupTelemetry(TracerProviderBuilder tracing);
}

