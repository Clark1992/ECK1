using Confluent.Kafka;
using ECK1.CommandsAPI.Commands;
using ECK1.Contracts.Kafka.BusinessEvents.Sample;
using ECK1.Kafka;
using ECK1.Kafka.Extensions;

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
            Confluent.SchemaRegistry.SubjectNameStrategy.Topic,
            SerializerType.JSON);

        #region Rebuild view

        services
            .ConfigSimpleTopicConsumer<Guid, SampleRebuildHandler>(
                kafkaSettings.BootstrapServers,
                kafkaSettings.SampleEventsRebuildRequestTopic,
                kafkaSettings.GroupId,
                Guid.Parse,
                c => c.WithAuth(kafkaSettings.User, kafkaSettings.Secret));

        #endregion

        return services;
    }
}
