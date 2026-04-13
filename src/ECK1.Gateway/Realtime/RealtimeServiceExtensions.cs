using Confluent.Kafka;
using Confluent.SchemaRegistry;
using ECK1.Kafka;
using ECK1.Kafka.Extensions;
using ECK1.RealtimeFeedback.Contracts;

namespace ECK1.Gateway.Realtime;

public static class RealtimeServiceExtensions
{
    public static IServiceCollection AddRealtimeNotifications(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.Configure<RealtimeConfig>(config.GetSection(RealtimeConfig.Section));

        var realtimeConfig = config.GetSection(RealtimeConfig.Section).Get<RealtimeConfig>()
            ?? new RealtimeConfig();

        if (string.IsNullOrEmpty(realtimeConfig.FeedbackTopic))
            throw new InvalidOperationException("Realtime:FeedbackTopic must be configured.");

        var signalrBuilder = services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = true;
            options.MaximumReceiveMessageSize = 32 * 1024; // 32 KB
            options.KeepAliveInterval = TimeSpan.FromSeconds(15);
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
        });

        if (!string.IsNullOrEmpty(realtimeConfig.RedisConnectionString))
        {
            signalrBuilder.AddStackExchangeRedis(realtimeConfig.RedisConnectionString, options =>
            {
                options.Configuration.ChannelPrefix =
                    StackExchange.Redis.RedisChannel.Literal("ECK1Realtime");
            });
        }

        services.AddSingleton<IRealtimeConnectionManager, RealtimeConnectionManager>();
        services.AddSingleton<IKafkaMessageHandler<RealtimeFeedbackEvent>, RealtimeFeedbackRouter>();

        var kafkaSettings = config.GetSection(KafkaSettings.Section).Get<KafkaSettings>()
            ?? throw new InvalidOperationException($"'{KafkaSettings.Section}' configuration section is required.");

        if (string.IsNullOrEmpty(kafkaSettings.BootstrapServers))
            throw new InvalidOperationException($"'{KafkaSettings.Section}:BootstrapServers' must be configured.");

        services.ConfigTopicConsumer<RealtimeFeedbackEvent>(
            kafkaSettings.BootstrapServers,
            realtimeConfig.FeedbackTopic,
            "gateway-realtime-feedback",
            SubjectNameStrategy.Topic,
            SerializerType.JSON,
            c =>
            {
                c.AutoOffsetReset = AutoOffsetReset.Latest;
                if (!string.IsNullOrEmpty(kafkaSettings.User))
                    c.WithAuth(kafkaSettings.User, kafkaSettings.Secret);
            });

        return services;
    }

    public static WebApplication MapRealtimeEndpoints(this WebApplication app)
    {
        app.MapHub<RealtimeHub>("/hubs/realtime");
        return app;
    }
}
