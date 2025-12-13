using Confluent.Kafka;
using Confluent.SchemaRegistry;
using ECK1.Kafka;
using ECK1.Kafka.Extensions;
using ECK1.Orleans.Extensions;
using ECK1.ViewProjector.Kafka.Orleans;
using ECK1.ViewProjector.Views;
using ECK1.ViewProjector.Events;

using Contract = ECK1.Contracts.Kafka;
using ECK1.IntegrationContracts.Kafka.IntegrationRecords.Sample;
using ECK1.IntegrationContracts.Kafka.IntegrationRecords;

namespace ECK1.ViewProjector.Kafka;

public static class KafkaSetup
{
    public static IServiceCollection SetupKafka(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton(
            typeof(IKafkaMessageHandler<Contract.BusinessEvents.Sample.ISampleEvent>),
            typeof(OrleansKafkaAdapter<Contract.BusinessEvents.Sample.ISampleEvent, ISampleEvent, SampleEventKafkaMetadata>));

        services.AddKafkaGrainRouter<
            ISampleEvent,
            SampleEventKafkaMetadata,
            SampleView,
            KafkaGrainHandler<ISampleEvent, SampleView>>(
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

        services.ConfigTopicConsumer<Contract.BusinessEvents.Sample.ISampleEvent>(
            kafkaSettings.BootstrapServers,
            kafkaSettings.SampleBusinessEventsTopic,
            kafkaSettings.GroupId,
            SubjectNameStrategy.Topic,
            SerializerType.JSON,
            c => c.WithAuth(kafkaSettings.User, kafkaSettings.Secret));

        #region Producers

        services
            .AddKafkaRootProducer(kafkaSettings.BootstrapServers,
            c =>
            {
                c.Acks = Acks.Leader;
                c.WithAuth(kafkaSettings.User, kafkaSettings.Secret);
                c.AllowAutoCreateTopics = true;
            });

        services.ConfigTopicProducer<Contract.BusinessEvents.Sample.SampleEventFailure>(
            kafkaSettings.SampleFailureEventsTopic,
            SubjectNameStrategy.Topic,
            SerializerType.JSON);

        services.ConfigTopicProducer<SampleThinEvent>(
            kafkaSettings.SampleThinEventsTopic,
            SubjectNameStrategy.Record,
            SerializerType.AVRO);

        services.ConfigTopicProducer<SampleFullRecord>(
            kafkaSettings.SampleFullRecordsTopic,
            SubjectNameStrategy.Topic,
            SerializerType.PROTO);

        #endregion

        return services;
    }
}
