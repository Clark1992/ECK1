using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Microsoft.Extensions.DependencyInjection;
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
        config.SecurityProtocol = SecurityProtocol.SaslSsl;
        config.SaslMechanism = SaslMechanism.Plain;
        config.SaslUsername = user;
        config.SaslPassword = secret;

        return config;
    }

    public static SchemaRegistryConfig WithAuth(this SchemaRegistryConfig config, string user, string secret)
    {
        config.BasicAuthCredentialsSource = AuthCredentialsSource.UserInfo;
        config.BasicAuthUserInfo = $"{user}:{secret}";

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

    public static IServiceCollection ConfigTopicConsumer<T>(
        this IServiceCollection services,
        string bootstrapServers,
        string topic,
        string groupId,
        SubjectNameStrategy strategy,
        SerializerType serializer,
        Action<ConsumerConfig> configAction = null)
        where T : class
    {
        services.AddSingleton<IKafkaTopicConsumer>(sp =>
        {
            var sr = sp.GetRequiredService<ISchemaRegistryClient>();
            var logger = sp.GetRequiredService<ILogger<KafkaJsonTopicConsumer<T>>>();
            var handler = sp.GetRequiredService<IMessageHandler<T>>();

            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = bootstrapServers,
                GroupId = groupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
            };

            configAction.Invoke(consumerConfig);

            return serializer switch
            {
                SerializerType.JSON => new KafkaJsonTopicConsumer<T>(consumerConfig, topic, sr, strategy, handler, logger),
                //Serializer.AVRO => new KafkaAvroTopicProducer<T>(root.Handle, topic, sr, strategy),
                _ => throw new InvalidOperationException("Unknown serializer format")
            };
        });

        return services;
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
}
