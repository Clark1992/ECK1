using Confluent.Kafka;
using Confluent.SchemaRegistry;
using ECK1.Contracts.Kafka.BusinessEvents;
using ECK1.Integration.Config;
using ECK1.Kafka;
using ECK1.Kafka.Extensions;
using ECK1.Reconciliation.Contracts;

namespace ECK1.FailedViewRebuilder.Kafka;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection SetupKafka(
        this IServiceCollection services,
        IConfiguration config,
        IntegrationConfig integrationConfig)
    {
        var kafkaSettings = config
            .GetSection(KafkaSettings.Section)
            .Get<KafkaSettings>();

        services.Configure<KafkaSettings>(config.GetSection(KafkaSettings.Section));

        services
            .WithSchemaRegistry(kafkaSettings.SchemaRegistryUrl,
                c => c.WithAuth(kafkaSettings.User, kafkaSettings.Secret));

        services.AddScoped<IKafkaMessageHandler<EventFailure>, EventFailuresHandler>();

        services.ConfigTopicConsumer<EventFailure>(
            kafkaSettings.BootstrapServers,
            kafkaSettings.FailureEventsTopic,
            kafkaSettings.GroupId,
            SubjectNameStrategy.Topic,
            SerializerType.JSON,
            c => c.WithAuth(kafkaSettings.User, kafkaSettings.Secret));

        services
            .AddKafkaRootProducer(kafkaSettings.BootstrapServers,
            c =>
            {
                c.Acks = Acks.Leader;
                c.WithAuth(kafkaSettings.User, kafkaSettings.Secret);
                c.AllowAutoCreateTopics = true;
            });

        // Keyed producers: one per entity type, targeting that entity's RebuildRequestTopic from manifest
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