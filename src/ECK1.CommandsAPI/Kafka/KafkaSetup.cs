using Confluent.Kafka;
using Confluent.SchemaRegistry;
using ECK1.CommandsAPI.Commands;
using ECK1.Contracts.Kafka.BusinessEvents.Sample;
using ECK1.Kafka.Extensions;
using Sample2Contracts = ECK1.Contracts.Kafka.BusinessEvents.Sample2;

namespace ECK1.CommandsAPI.Kafka;

public static class KafkaSetup
{
    public static IServiceCollection SetupKafka(this IServiceCollection services, IConfiguration config)
    {
        var kafkaSettings = config
            .GetSection(KafkaSettings.Section)
            .Get<KafkaSettings>();

        services
            .AddKafkaRootProducer(kafkaSettings.BootstrapServers,
            c =>
            {
                c.Acks = Acks.Leader;
                c.WithAuth(kafkaSettings.User, kafkaSettings.Secret);
            })
            .WithSchemaRegistry(kafkaSettings.SchemaRegistryUrl,
                c => c.WithAuth(kafkaSettings.User, kafkaSettings.Secret));

        services.ConfigTopicProducer<ISampleEvent>(
            kafkaSettings.SampleBusinessEventsTopic,
            SubjectNameStrategy.Topic,
            SerializerType.JSON);

        services.ConfigTopicProducer<Sample2Contracts.ISample2Event>(
            kafkaSettings.Sample2BusinessEventsTopic,
            SubjectNameStrategy.Topic,
            SerializerType.JSON);

        #region Rebuild view

        services
            .ConfigSimpleTopicConsumer<Guid, RebuildHandler<RebuildSampleViewCommand>>(
                kafkaSettings.BootstrapServers,
                kafkaSettings.SampleEventsRebuildRequestTopic,
                kafkaSettings.GroupId,
                Guid.Parse,
                c => c.WithAuth(kafkaSettings.User, kafkaSettings.Secret));

        services
            .ConfigSimpleTopicConsumer<Guid, RebuildHandler<RebuildSample2ViewCommand>>(
                kafkaSettings.BootstrapServers,
                kafkaSettings.Sample2EventsRebuildRequestTopic,
                kafkaSettings.GroupId,
                Guid.Parse,
                c => c.WithAuth(kafkaSettings.User, kafkaSettings.Secret));

        #endregion

        return services;
    }
}
