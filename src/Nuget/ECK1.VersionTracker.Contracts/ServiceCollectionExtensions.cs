using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProtoBuf.Grpc.Client;

namespace ECK1.VersionTracker.Contracts;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddVersionTrackerClient(
        this IServiceCollection services,
        IConfiguration configuration,
        string configSectionName = "VersionTracker")
    {
        var url = configuration[$"{configSectionName}:Url"]
            ?? throw new InvalidOperationException($"{configSectionName}:Url is not configured.");

        var channel = GrpcChannel.ForAddress(url, new GrpcChannelOptions
        {
            HttpHandler = new SocketsHttpHandler
            {
                PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
                KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
                EnableMultipleHttp2Connections = true
            }
        });

        services.AddSingleton(channel);
        services.AddSingleton(channel.CreateGrpcService<IVersionTrackerService>());

        return services;
    }
}
