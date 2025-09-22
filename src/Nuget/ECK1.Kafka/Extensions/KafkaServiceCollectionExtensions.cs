using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace ECK1.Kafka.Extensions;

public static class KafkaServiceCollectionExtensions
{
    private enum Serializer
    {
        JSON = 0,
        AVRO
    };

    private static Serializer serializer = Serializer.JSON;

    public static IServiceCollection AddKafkaRoot(
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

    public static ProducerConfig WithAuth(this ProducerConfig config, string user, string secret)
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

    public static IServiceCollection WithJSONSerializer(this IServiceCollection services,
        string schemaRegistryUrl,
        Action<SchemaRegistryConfig> configAction = null)
    {
        serializer = Serializer.JSON;

        SetupSchemaRegistry(services, schemaRegistryUrl, configAction);

        return services;
    }

    public static IServiceCollection WithAVROSerializer(this IServiceCollection services, 
        string schemaRegistryUrl,
        Action<SchemaRegistryConfig> configAction = null)
    {
        serializer = Serializer.AVRO;

        SetupSchemaRegistry(services, schemaRegistryUrl, configAction);

        return services;
    }

    public static IServiceCollection ConfigTopicForProducer<T>(
        this IServiceCollection services,
        string topic)
        where T : class
    {
        services.AddSingleton<IKafkaTopicProducer<T>>(sp =>
        {
            var root = sp.GetRequiredService<IProducer<string, string>>();
            var sr = sp.GetRequiredService<ISchemaRegistryClient>();
            return serializer switch
            {
                Serializer.JSON => new KafkaJsonTopicProducer<T>(root.Handle, topic, sr),
                Serializer.AVRO => new KafkaAvroTopicProducer<T>(root.Handle, topic, sr),
                _ => throw new InvalidOperationException("unknown serializer")
            }; 
        });

        return services;
    }

    private static void SetupSchemaRegistry(IServiceCollection services, string schemaRegistryUrl, Action<SchemaRegistryConfig> configAction)
    {
        var config = new SchemaRegistryConfig
        {
            Url = schemaRegistryUrl
        };

        configAction.Invoke(config);

        services.AddSingleton<ISchemaRegistryClient>(_ =>
            new CachedSchemaRegistryClient(config));
    }
}
