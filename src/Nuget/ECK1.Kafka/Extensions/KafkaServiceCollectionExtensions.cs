using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ECK1.Kafka.Extensions;

public enum SerializerType
{
    JSON = 0,
    AVRO
};

public static class KafkaServiceCollectionExtensions
{
    public static IServiceCollection AddKafkaRootProducer(
        this IServiceCollection services,
        string bootstrapServers,
        Action<ProducerConfig> configAction = null)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = bootstrapServers
        };

        configAction?.Invoke(config);

        services.AddSingleton(_ =>
            new ProducerBuilder<string, byte[]>(config).Build());

        return services;
    }

    public static TConfig WithAuth<TConfig>(this TConfig config, string user, string secret)
        where TConfig : ClientConfig
    {
#if DEBUG
        config.SecurityProtocol = SecurityProtocol.SaslSsl;

        config.AllowAutoCreateTopics = true;
        config.SslEndpointIdentificationAlgorithm = SslEndpointIdentificationAlgorithm.None;
        config.EnableSslCertificateVerification = false;
#else
        config.SecurityProtocol = SecurityProtocol.SaslPlaintext;
#endif
        config.SaslMechanism = SaslMechanism.ScramSha512;
        config.SaslUsername = user;
        config.SaslPassword = secret;

        return config;
    }

    public static SchemaRegistryConfig WithAuth(this SchemaRegistryConfig config, string user, string secret)
    {
#if DEBUG
        config.BasicAuthCredentialsSource = AuthCredentialsSource.UserInfo;
        config.BasicAuthUserInfo = $"{user}:{secret}";
#endif

        return config;
    }

    public static IServiceCollection ConfigTopicProducer<T>(
        this IServiceCollection services,
        string topic,
        SubjectNameStrategy strategy,
        SerializerType serializer)
        where T : class
    {
        services.AddSingleton<IKafkaTopicProducer<T>>(sp =>
        {
            var root = sp.GetRequiredService<IProducer<string, byte[]>>();
            var sr = sp.GetRequiredService<ISchemaRegistryClient>();
            return serializer switch
            {
                SerializerType.JSON => new KafkaJsonTopicProducer<T>(root.Handle, topic, sr, strategy, sp.GetRequiredService<ILogger<KafkaJsonTopicProducer<T>>>()),
                SerializerType.AVRO => new KafkaAvroTopicProducer<T>(root.Handle, topic, sr, strategy, sp.GetRequiredService<ILogger<KafkaAvroTopicProducer<T>>>()),
                _ => throw new InvalidOperationException("Unknown serializer format")
            }; 
        });

        return services;
    }

    public static IServiceCollection ConfigSimpleTopicProducer<T>(this IServiceCollection services)
    {
        services.AddSingleton<IKafkaSimpleProducer<T>>(sp =>
        {
            var root = sp.GetRequiredService<IProducer<string, byte[]>>();
            return new KafkaSimpleProducer<T>(root.Handle, sp.GetRequiredService<ILogger<KafkaSimpleProducer<T>>>());
        });

        return services;
    }

    public static IServiceCollection ConfigTopicConsumer<T>(
        this IServiceCollection services,
        string bootstrapServers,
        string topic,
        string groupId,
        SubjectNameStrategy strategy,
        SerializerType serializer,
        Action<ConsumerConfig> configAction = null)
        where T : class =>
        ConfigTopicConsumer<T>(
            services,
            bootstrapServers,
            topic,
            groupId,
            strategy,
            serializer,
            consumer => consumer.WithHandler<IKafkaMessageHandler<T>>(),
            configAction);

    public static IServiceCollection ConfigTopicConsumer<T>(
        this IServiceCollection services,
        string bootstrapServers,
        string topic,
        string groupId,
        SubjectNameStrategy strategy,
        SerializerType serializer,
        Func<string, T, KafkaMessageId, CancellationToken, Task> handler,
        Action<ConsumerConfig> configAction = null)
        where T : class =>
        ConfigTopicConsumer<T>(
            services,
            bootstrapServers,
            topic,
            groupId,
            strategy,
            serializer,
            (consumer) => consumer.WithHandler(handler),
            configAction);

    private static IServiceCollection ConfigTopicConsumer<T>(
        IServiceCollection services,
        string bootstrapServers,
        string topic,
        string groupId,
        SubjectNameStrategy strategy,
        SerializerType serializer,
        Action<IHandlerConfigurator<T>> configHandler,
        Action<ConsumerConfig> configAction = null)
        where T : class
    {
        services.AddSingleton<IKafkaTopicConsumer>(sp =>
        {
            var sr = sp.GetRequiredService<ISchemaRegistryClient>();
            var logger = sp.GetRequiredService<ILogger<KafkaJsonTopicConsumer<T>>>();
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = bootstrapServers,
                GroupId = groupId,
                EnableAutoCommit = false,
                AutoOffsetReset = AutoOffsetReset.Earliest,
            };

            configAction?.Invoke(consumerConfig);

            var consumer = serializer switch
            {
                SerializerType.JSON => new KafkaJsonTopicConsumer<T>(consumerConfig, topic, sr, strategy, logger, scopeFactory),
                //SerializerType.AVRO => new KafkaAvroTopicProducer<T>(root.Handle, topic, sr, strategy),
                _ => throw new InvalidOperationException("Unknown serializer format")
            };

            configHandler(consumer);

            return consumer;
        });
        
        AddListenerService(services);

        return services;
    }

    private static void AddListenerService(IServiceCollection services)
    {
        if (!services.Any(sd => sd.ServiceType == typeof(IHostedService) &&
                                sd.ImplementationType == typeof(KafkaTopicConsumerService)))
        {
            services.AddHostedService<KafkaTopicConsumerService>();
        }
    }

    public static IServiceCollection WithSchemaRegistry(this IServiceCollection services, string schemaRegistryUrl, Action<SchemaRegistryConfig> configAction)
    {
        var config = new SchemaRegistryConfig
        {
            Url = schemaRegistryUrl
        };

        configAction.Invoke(config);

        services.AddSingleton<ISchemaRegistryClient>(_ =>
            new CachedSchemaRegistryClient(config));

        return services;
    }

    public static IServiceCollection ConfigSimpleTopicConsumer<TValue, THandler>(
        this IServiceCollection services,
        string bootstrapServers,
        string topic,
        string groupId,
        Func<string, TValue> parser,
        Action<ConsumerConfig> configAction = null)
        where THandler: class, IKafkaMessageHandler<TValue>
    {
        services.AddScoped<THandler>();
        services.AddSingleton<IKafkaTopicConsumer>(sp =>
        {
            var sr = sp.GetRequiredService<ISchemaRegistryClient>();
            var logger = sp.GetRequiredService<ILogger<KafkaSimpleTopicConsumer<TValue>>>();
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = bootstrapServers,
                GroupId = groupId,
                EnableAutoCommit = false,
                AutoOffsetReset = AutoOffsetReset.Earliest,
            };

            configAction?.Invoke(consumerConfig);

            KafkaSimpleTopicConsumer<TValue> consumer = new(consumerConfig, topic, parser, logger, scopeFactory);

            consumer.WithHandler<THandler>();

            return consumer;
        });

        AddListenerService(services);

        return services;
    }
}
