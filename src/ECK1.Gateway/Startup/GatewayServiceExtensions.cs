using Confluent.Kafka;
using Confluent.SchemaRegistry;
using ECK1.Gateway.Commands;
using ECK1.Gateway.Discovery;
using ECK1.Gateway.Proxy;
using ECK1.Gateway.Swagger;
using ECK1.Kafka;
using ECK1.Kafka.Extensions;
using k8s;
using Newtonsoft.Json.Linq;
using Yarp.ReverseProxy.Configuration;

namespace ECK1.Gateway.Startup;

public static class GatewayServiceExtensions
{
    public static IServiceCollection AddGatewayOptions(
        this IServiceCollection services, IConfiguration config)
    {
        services.Configure<GatewayConfig>(config.GetSection(GatewayConfig.Section));
        services.Configure<KafkaSettings>(config.GetSection(KafkaSettings.Section));
        return services;
    }

    public static IServiceCollection AddServiceDiscovery(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddHttpClient("ServiceDiscovery", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        var gatewayConfig = config.GetSection(GatewayConfig.Section).Get<GatewayConfig>()
            ?? new GatewayConfig();

        if (gatewayConfig.StaticServices.Count > 0)
        {
            services.AddSingleton<IServiceDiscovery, StaticServiceDiscovery>();
        }
        else
        {
            services.AddSingleton<IKubernetes>(_ =>
            {
                var k8sConfig = KubernetesClientConfiguration.InClusterConfig();
                return new Kubernetes(k8sConfig);
            });
            services.AddSingleton<IServiceDiscovery, KubernetesServiceDiscovery>();
        }

        services.AddSingleton<ISwaggerDiscoveryService, SwaggerDiscoveryService>();
        services.AddSingleton<IAsyncApiDiscoveryService, AsyncApiDiscoveryService>();
        services.AddSingleton<ServiceRouteState>();
        services.AddSingleton<CommandRouteState>();

        return services;
    }

    public static IServiceCollection AddGatewayProxy(this IServiceCollection services)
    {
        services.AddSingleton<DynamicYarpConfigProvider>();
        services.AddSingleton<IProxyConfigProvider>(sp =>
            sp.GetRequiredService<DynamicYarpConfigProvider>());
        services.AddReverseProxy();
        return services;
    }

    public static IServiceCollection AddGatewaySwagger(this IServiceCollection services)
    {
        services.AddSingleton<SwaggerAggregator>();
        return services;
    }

    public static IServiceCollection AddCommandPipeline(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<HttpRequestCommandBinder>();
        services.AddScoped<CommandEndpointHandler>();
        services.AddSingleton<DynamicCommandEndpointDataSource>();

        var kafkaSettings = config.GetSection(KafkaSettings.Section).Get<KafkaSettings>();
        if (kafkaSettings is not null && !string.IsNullOrEmpty(kafkaSettings.BootstrapServers))
        {
            services
                .AddKafkaRootProducer(kafkaSettings.BootstrapServers, c =>
                {
                    c.Acks = Acks.Leader;
                    if (!string.IsNullOrEmpty(kafkaSettings.User))
                        c.WithAuth(kafkaSettings.User, kafkaSettings.Secret);
                })
                .WithSchemaRegistry(kafkaSettings.SchemaRegistryUrl, c =>
                {
                    if (!string.IsNullOrEmpty(kafkaSettings.User))
                        c.WithAuth(kafkaSettings.User, kafkaSettings.Secret);
                })
                .ConfigProducer<JObject>(SubjectNameStrategy.Topic, SerializerType.JSON);
        }
        else
        {
            services.AddSingleton<IKafkaProducer<JObject>, NoOpKafkaProducer<JObject>>();
        }

        return services;
    }
}
