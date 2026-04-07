using Confluent.Kafka;
using Confluent.SchemaRegistry;
using ECK1.Gateway.Commands;
using ECK1.Gateway.Discovery;
using ECK1.Gateway.Proxy;
using ECK1.Gateway.Swagger;
using ECK1.Kafka;
using ECK1.Kafka.Extensions;
using k8s;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
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
        services.Configure<ZitadelConfig>(config.GetSection(ZitadelConfig.Section));
        return services;
    }

    public static IServiceCollection AddGatewayAuth(
        this IServiceCollection services, IConfiguration config)
    {
        var zitadelConfig = config.GetSection(ZitadelConfig.Section).Get<ZitadelConfig>();
        if (zitadelConfig is null || string.IsNullOrEmpty(zitadelConfig.Authority))
            return services;

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                // Use the external Issuer URL so ZitadelBackchannelHandler can
                // intercept and rewrite it to the internal Authority URL.
                var metadataBase = !string.IsNullOrEmpty(zitadelConfig.Issuer)
                    ? zitadelConfig.Issuer : zitadelConfig.Authority;
                options.MetadataAddress = $"{metadataBase.TrimEnd('/')}/.well-known/openid-configuration";
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = false,
                    ValidateIssuer = !string.IsNullOrEmpty(zitadelConfig.Issuer),
                    ValidIssuer = zitadelConfig.Issuer,
                    NameClaimType = "preferred_username"
                };

                // Rewrite JWKS/discovery URLs from external domain to internal authority
                // so the gateway can validate tokens without reaching the external URL.
                if (!string.IsNullOrEmpty(zitadelConfig.Issuer)
                    && zitadelConfig.Issuer != zitadelConfig.Authority)
                {
                    options.BackchannelHttpHandler = new ZitadelBackchannelHandler(
                        zitadelConfig.Issuer.TrimEnd('/'),
                        zitadelConfig.Authority.TrimEnd('/'));
                }
            });

        services.AddTransient<IClaimsTransformation, ZitadelRoleClaimsTransformation>();

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
#if DEBUG
                var k8sConfig = KubernetesClientConfiguration.BuildConfigFromConfigFile();
#else
                var k8sConfig = KubernetesClientConfiguration.InClusterConfig();
#endif
                return new Kubernetes(k8sConfig);
            });
            services.AddSingleton<IServiceDiscovery, KubernetesServiceDiscovery>();
        }

        services.AddSingleton<ISwaggerDiscoveryService, SwaggerDiscoveryService>();
        services.AddSingleton<IAsyncApiDiscoveryService, AsyncApiDiscoveryService>();
        services.AddSingleton<ServiceRouteState>();
        services.AddSingleton<CommandRouteState>();
        services.AddSingleton<RouteAuthorizationState>();

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
