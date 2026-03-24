using Confluent.Kafka;
using Confluent.SchemaRegistry;
using ECK1.CommandsAPI.Commands;
using ECK1.Integration.Config;
using ECK1.IntegrationContracts.Kafka.IntegrationRecords.Generated;
using ECK1.Kafka;
using ECK1.Kafka.Extensions;
using ECK1.Orleans.Grains;

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

        services.ConfigSimpleTopicConsumer<Guid, RebuildHandler<RebuildSampleViewCommand>>(
            kafkaSettings.BootstrapServers,
            kafkaSettings.SampleEventsRebuildRequestTopic,
            kafkaSettings.GroupId,
            Guid.Parse,
            c => c.WithAuth(kafkaSettings.User, kafkaSettings.Secret));

        services.ConfigSimpleTopicConsumer<Guid, RebuildHandler<RebuildSample2ViewCommand>>(
            kafkaSettings.BootstrapServers,
            kafkaSettings.Sample2EventsRebuildRequestTopic,
            kafkaSettings.GroupId,
            Guid.Parse,
            c => c.WithAuth(kafkaSettings.User, kafkaSettings.Secret));

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
