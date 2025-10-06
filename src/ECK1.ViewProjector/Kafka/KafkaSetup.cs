using Confluent.Kafka;
using Confluent.SchemaRegistry;
using ECK1.Kafka;
using ECK1.Kafka.Extensions;
using ECK1.Orleans.Extensions;
using Contract = ECK1.Contracts.Kafka.BusinessEvents;
using ViewEvent = ECK1.ViewProjector.Events;
using ECK1.ViewProjector.Kafka.Orleans;
using ECK1.ViewProjector.Views;
using ECK1.ViewProjector;
using ECK1.ViewProjector.Events;

namespace ECK1.ViewProjector.Kafka;

public static class KafkaSetup
{
    public static IServiceCollection SetupKafka(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton(
            typeof(IKafkaMessageHandler<Contract.Sample.ISampleEvent>),
            typeof(OrleansKafkaAdapter<Contract.Sample.ISampleEvent, ISampleEvent, SampleEventKafkaMetadata>));

        services.AddKafkaGrainRouter<
            ISampleEvent,
            SampleEventKafkaMetadata,
            SampleView,
            KafkaMessageHandler<ISampleEvent, SampleView>>(
            ev => ev.SampleId.ToString())
            .AddDupChecker<SampleEventKafkaMetadata>()
            .AddMetadataUpdater<SampleEventKafkaMetadata>()
            .AddFaultedStateReset<SampleEventKafkaMetadata>();

        var kafkaSettings = config
            .GetSection(KafkaSettings.Section)
            .Get<KafkaSettings>();

        services
            .WithSchemaRegistry(kafkaSettings.SchemaRegistryUrl,
                c => c.WithAuth(kafkaSettings.User, kafkaSettings.Secret));

        services.ConfigTopicConsumer<Contract.Sample.ISampleEvent>(
            kafkaSettings.BootstrapServers,
            kafkaSettings.SampleBusinessEventsTopic,
            kafkaSettings.GroupId,
            SubjectNameStrategy.Topic,
            SerializerType.JSON,
            c =>
            {
                c.WithAuth(kafkaSettings.User, kafkaSettings.Secret);
            });

        services.AddHostedService<KafkaTopicConsumerService>();

        #region Events Failure producer

        services
            .AddKafkaRootProducer(kafkaSettings.BootstrapServers,
            c =>
            {
                c.Acks = Acks.Leader;
                c.WithAuth(kafkaSettings.User, kafkaSettings.Secret);
                c.AllowAutoCreateTopics = true;
            });

        services.ConfigTopicProducer<Contract.Sample.SampleEventFailure>(
            kafkaSettings.SampleFailureEventsTopic,
            SubjectNameStrategy.Topic,
            SerializerType.JSON);

        #endregion

        return services;
    }
}
