using Confluent.Kafka;
using Confluent.SchemaRegistry;
using ECK1.Reconciliation.Contracts;
using ECK1.Integration.Config;
using ECK1.IntegrationContracts.Kafka.IntegrationRecords.Generated;
using ECK1.Kafka;
using ECK1.Kafka.Extensions;

namespace ECK1.Reconciler.Kafka;

public static class KafkaSetup
{
    public static IServiceCollection SetupKafka(
        this IServiceCollection services,
        IConfiguration config,
        IntegrationConfig integrationConfig)
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

        // Producers
        services.ConfigTopicProducer<ReconcileRequest>(
            kafkaSettings.ReconcileRequestsTopic,
            SubjectNameStrategy.Topic,
            SerializerType.JSON);

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

        // Consumer: ReconcileResult (from proxies)
        services.AddScoped<IKafkaMessageHandler<ReconcileResult>, ReconcileResultHandler>();
        services.ConfigTopicConsumer<ReconcileResult>(
            kafkaSettings.BootstrapServers,
            kafkaSettings.ReconcileResultsTopic,
            kafkaSettings.GroupId,
            SubjectNameStrategy.Topic,
            SerializerType.JSON,
            c => c.WithAuth(kafkaSettings.User, kafkaSettings.Secret));

        // Consumers: ThinEvent (one per entity type from integration manifest)
        foreach (var (_, entry) in integrationConfig)
        {
            services.ConfigTopicConsumer<ThinEvent>(
                kafkaSettings.BootstrapServers,
                entry.EventsTopic,
                kafkaSettings.GroupId,
                SubjectNameStrategy.Record,
                SerializerType.AVRO,
                sp =>
                {
                    var handler = sp.GetRequiredService<ThinEventHandler>();
                    return (key, @event, messageId, ct) =>
                        handler.HandleAsync(entry.EntityType, @event, ct);
                },
                c =>
                {
                    c.WithAuth(kafkaSettings.User, kafkaSettings.Secret);
                    c.AutoOffsetReset = AutoOffsetReset.Earliest;
                    c.EnableAutoCommit = false;
                });
        }

        services.AddScoped<ThinEventHandler>();

        return services;
    }
}
