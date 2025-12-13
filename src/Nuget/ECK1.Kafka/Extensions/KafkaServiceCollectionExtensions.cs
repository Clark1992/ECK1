using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using ECK1.Kafka.ProtoBuf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ECK1.Kafka.Extensions;

public enum SerializerType
{
    JSON = 0,
    AVRO,
    PROTO
};

public static class KafkaServiceCollectionExtensions
{
    private const string UnkwonFormat = "Unknown serializer format";

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
        SerializerType format)
        where T : class
    {
        services.AddSingleton<IKafkaTopicProducer<T>>(sp =>
        {
            var root = sp.GetRequiredService<IProducer<string, byte[]>>();
            var schemaRegistry = sp.GetRequiredService<ISchemaRegistryClient>();

            IAsyncSerializer<T> serializer = format switch
            {
                SerializerType.JSON => new JsonSerializer<T>(
                        schemaRegistry,
                        new JsonSerializerConfig
                        {
                            AutoRegisterSchemas = false,
                            UseLatestVersion = true,
                            SubjectNameStrategy = strategy
                        }),

                SerializerType.AVRO => new AvroSerializer<T>(
                        schemaRegistry,
                        new()
                        {
                            AutoRegisterSchemas = false,
                            UseLatestVersion = true,
                            SubjectNameStrategy = strategy
                        }),

                SerializerType.PROTO => new ProtobufNetSerializer<T>(
                        schemaRegistry,
                        sp.GetRequiredService<ILogger<ProtobufNetSerializer<T>>>()),

                _ => throw new InvalidOperationException(UnkwonFormat)
            };

            return format switch
            {
                SerializerType.JSON => new KafkaJsonTopicProducer<T>(root.Handle, topic, serializer, sp.GetRequiredService<ILogger<KafkaJsonTopicProducer<T>>>()),
                SerializerType.AVRO => new KafkaAvroTopicProducer<T>(root.Handle, topic, serializer, sp.GetRequiredService<ILogger<KafkaAvroTopicProducer<T>>>()),
                SerializerType.PROTO => new KafkaProtoTopicProducer<T>(root.Handle, topic, serializer, sp.GetRequiredService<ILogger<KafkaProtoTopicProducer<T>>>()),
                _ => throw new InvalidOperationException(UnkwonFormat)
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

    public static IServiceCollection ConfigRawBytesTopicProducer(this IServiceCollection services)
    {
        services.AddSingleton<IKafkaRawBytesProducer>(sp =>
        {
            var root = sp.GetRequiredService<IProducer<string, byte[]>>();
            return new KafkaRawBytesProducer(root.Handle, sp.GetRequiredService<ILogger<KafkaRawBytesProducer>>());
        });

        return services;
    }

    public static IServiceCollection ConfigTopicConsumer<T>(
        this IServiceCollection services,
        string bootstrapServers,
        string topic,
        string groupId,
        SubjectNameStrategy strategy,
        SerializerType format,
        Action<ConsumerConfig> configAction = null)
        where T : class =>
        ConfigTopicConsumer<T>(
            services,
            bootstrapServers,
            topic,
            groupId,
            strategy,
            format,
            consumer => consumer.WithHandler<IKafkaMessageHandler<T>>(),
            configAction);

    public static IServiceCollection ConfigTopicConsumer<T>(
        this IServiceCollection services,
        string bootstrapServers,
        string topic,
        string groupId,
        SubjectNameStrategy strategy,
        SerializerType format,
        Func<string, T, KafkaMessageId, CancellationToken, Task> handler,
        Action<ConsumerConfig> configAction = null)
        where T : class =>
        ConfigTopicConsumer<T>(
            services,
            bootstrapServers,
            topic,
            groupId,
            strategy,
            format,
            consumer => consumer.WithHandler(handler),
            configAction);

    public static IServiceCollection ConfigTopicConsumer<T>(
        this IServiceCollection services,
        string bootstrapServers,
        string topic,
        string groupId,
        SubjectNameStrategy strategy,
        SerializerType format,
        Func<IServiceProvider, Func<string, T, KafkaMessageId, CancellationToken, Task>> handler,
        Action<ConsumerConfig> configAction = null)
        where T : class =>
        ConfigTopicConsumer<T>(
            services,
            bootstrapServers,
            topic,
            groupId,
            strategy,
            format,
            consumer => consumer.WithHandler(handler),
            configAction);

    private static IServiceCollection ConfigTopicConsumer<T>(
        IServiceCollection services,
        string bootstrapServers,
        string topic,
        string groupId,
        SubjectNameStrategy strategy,
        SerializerType format,
        Action<IHandlerConfigurator<T>> configHandler,
        Action<ConsumerConfig> configAction = null)
        where T : class
    {
        services.AddSingleton<IKafkaTopicConsumer>(sp =>
        {
            var sr = sp.GetRequiredService<ISchemaRegistryClient>();
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = bootstrapServers,
                GroupId = groupId,
                EnableAutoCommit = false,
                AutoOffsetReset = AutoOffsetReset.Earliest,
            };

            configAction?.Invoke(consumerConfig);

            var schemaRegistry = sp.GetRequiredService<ISchemaRegistryClient>();

            IAsyncDeserializer <T> deserializer = format switch
            {
                SerializerType.JSON => new JsonDeserializer<T>(
                   schemaRegistry,
                   new JsonSerializerConfig
                   {
                       AutoRegisterSchemas = false,
                       UseLatestVersion = true,
                       SubjectNameStrategy = strategy
                   }),

                SerializerType.AVRO => new AvroDeserializer<T>(
                   schemaRegistry,
                   new()
                   {
                       UseLatestVersion = true,
                       SubjectNameStrategy = strategy
                   }),

                SerializerType.PROTO => new ProtobufNetDeserializer<T>(),

                _ => throw new InvalidOperationException("Unknown deserializer format")
            };

            KafkaConsumerBase<T> consumer = format switch
            {
                SerializerType.JSON => new KafkaJsonTopicConsumer<T>(consumerConfig, topic, deserializer, sp.GetRequiredService<ILogger<KafkaJsonTopicConsumer<T>>>(), scopeFactory),
                SerializerType.AVRO => new KafkaAvroTopicConsumer<T>(consumerConfig, topic, deserializer, sp.GetRequiredService<ILogger<KafkaAvroTopicConsumer<T>>>(), scopeFactory),
                SerializerType.PROTO => new KafkaProtoTopicConsumer<T>(consumerConfig, topic, deserializer, sp.GetRequiredService<ILogger<KafkaProtoTopicConsumer<T>>>(), scopeFactory),
                _ => throw new InvalidOperationException("Unknown deserializer format")
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
        Func<string, TValue> parser = null,
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

            KafkaSimpleTopicConsumer<TValue> consumer = new(consumerConfig, topic, logger, scopeFactory);

            consumer.WithHandler<THandler>();
            consumer.WithParser(parser);

            return consumer;
        });

        AddListenerService(services);

        return services;
    }

    public static IServiceCollection ConfigRawBytesTopicConsumer<TValue, THandler>(
        this IServiceCollection services,
        string bootstrapServers,
        string topic,
        string groupId,
        Func<byte[], TValue> parser = null,
        Action<ConsumerConfig> configAction = null)
        where THandler : class, IKafkaMessageHandler<TValue>
        where TValue : class
    {
        services.AddScoped<THandler>();
        services.AddSingleton<IKafkaTopicConsumer>(sp =>
        {
            var sr = sp.GetRequiredService<ISchemaRegistryClient>();
            var logger = sp.GetRequiredService<ILogger<KafkaRawBytesTopicConsumer<TValue>>>();
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = bootstrapServers,
                GroupId = groupId,
                EnableAutoCommit = false,
                AutoOffsetReset = AutoOffsetReset.Earliest,
            };

            configAction?.Invoke(consumerConfig);

            KafkaRawBytesTopicConsumer<TValue> consumer = new(
                consumerConfig,
                topic,
                logger,
                scopeFactory);

            consumer.WithHandler<THandler>();
            consumer.WithParser(parser);

            return consumer;
        });

        AddListenerService(services);

        return services;
    }
}
