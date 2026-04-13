using Confluent.Kafka;
using Confluent.SchemaRegistry;
using ECK1.Integration.Config;
using ECK1.Kafka;
using ECK1.Kafka.Extensions;
using ECK1.Reconciliation.Contracts;

namespace ECK1.VersionTracker.Kafka;

public static class KafkaSetup
{
    public static IServiceCollection SetupKafka(this IServiceCollection services, IConfiguration config)
    {
        var kafkaSettings = config
            .GetSection(KafkaSettings.Section)
            .Get<KafkaSettings>()
            ?? throw new InvalidOperationException("Kafka settings are missing.");

        services
            .AddKafkaRootProducer(kafkaSettings.BootstrapServers,
            c =>
            {
                c.Acks = Acks.Leader;
                c.WithAuth(kafkaSettings.User, kafkaSettings.Secret);
            })
            .WithSchemaRegistry(kafkaSettings.SchemaRegistryUrl,
                c => c.WithAuth(kafkaSettings.User, kafkaSettings.Secret));

        var integrationConfig = ConfigHelpers.LoadConfig(config);
        services.AddSingleton(integrationConfig);

        foreach (var (_, entry) in integrationConfig)
        {
            if (string.IsNullOrEmpty(entry.RebuildRequestTopic))
                continue;

            services.ConfigKeyedTopicProducer<RebuildRequest>(
                entry.EntityType,
                entry.RebuildRequestTopic,
                SubjectNameStrategy.Topic,
                SerializerType.JSON);
        }

        services.AddSingleton<IReadOnlyDictionary<string, IKafkaTopicProducer<RebuildRequest>>>(sp =>
        {
            var dict = new Dictionary<string, IKafkaTopicProducer<RebuildRequest>>();
            foreach (var (_, entry) in integrationConfig)
            {
                if (string.IsNullOrEmpty(entry.RebuildRequestTopic))
                    continue;

                dict[entry.EntityType] = sp.GetRequiredKeyedService<IKafkaTopicProducer<RebuildRequest>>(entry.EntityType);
            }
            return dict;
        });

        return services;
    }
}
