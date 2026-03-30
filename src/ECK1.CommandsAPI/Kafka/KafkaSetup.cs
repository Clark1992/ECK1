using Confluent.Kafka;
using Confluent.SchemaRegistry;
using ECK1.CommandsAPI.Commands;
using ECK1.Reconciliation.Contracts;
using ECK1.Integration.Config;
using ECK1.IntegrationContracts.Kafka.IntegrationRecords.Generated;
using ECK1.Kafka;
using ECK1.Kafka.Extensions;

using static ECK1.Integration.Config.ConfigHelpers;

namespace ECK1.CommandsAPI.Kafka;

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

        services.SetupEventIntegrations(config,
            setupThinEvent: (entityType, topic) =>
                services.ConfigKeyedTopicProducer<ThinEvent>(entityType, topic,
                    SubjectNameStrategy.Record, SerializerType.AVRO),
            setupFullRecord: (recordType, topic) =>
                services.ConfigTopicProducer(recordType, topic,
                    SubjectNameStrategy.Topic, SerializerType.PROTO));

        services.AddSingleton<IIntegrationEventProducerFactory, IntegrationEventProducerFactory>();

        services.AddCommands(config, kafkaSettings);

        services.AddKeyedScoped<IRebuildHandler, RebuildHandler<RebuildSampleViewCommand>>("ECK1.Sample");
        services.AddKeyedScoped<IRebuildHandler, RebuildHandler<RebuildSample2ViewCommand>>("ECK1.Sample2");

        // Register rebuild-request consumers per distinct RebuildRequestTopic from the manifest
        var integrationConfig = ConfigHelpers.LoadConfig(config);
        var rebuildTopics = integrationConfig.Values
            .Select(e => e.RebuildRequestTopic)
            .Where(t => !string.IsNullOrEmpty(t))
            .Distinct();

        foreach (var rebuildTopic in rebuildTopics)
        {
            services.ConfigTopicConsumer<RebuildRequest>(
                kafkaSettings.BootstrapServers,
                rebuildTopic,
                kafkaSettings.GroupId,
                SubjectNameStrategy.Topic,
                SerializerType.JSON,
                sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<RebuildHandler<RebuildViewCommandBase>>>();
                    return (key, message, messageId, ct) =>
                    {
                        var handler = sp.GetKeyedService<IRebuildHandler>(message.EntityType);
                        if (handler is null)
                        {
                            logger.LogWarning("No rebuild handler registered for entity type '{EntityType}'", message.EntityType);
                            return Task.CompletedTask;
                        }
                        return handler.HandleAsync(message, ct);
                    };
                },
                c => c.WithAuth(kafkaSettings.User, kafkaSettings.Secret));
        }

        return services;
    }

    public static IServiceCollection AddCommands(
        this IServiceCollection services,
        IConfiguration config,
        KafkaSettings kafkaSettings)
    {
        var commandConfig = new CommandConsumerConfig(services, config, kafkaSettings);
        AsyncApi.Generated.CommandConfigurator.AddCommands(commandConfig);
        return services;
    }
}
