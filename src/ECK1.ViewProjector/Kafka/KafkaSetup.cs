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
using ECK1.IntegrationContracts.Kafka.IntegrationRecords.Sample2;
using ECK1.IntegrationContracts.Kafka.IntegrationRecords;

namespace ECK1.ViewProjector.Kafka;

public static class KafkaSetup
{
    public static IServiceCollection SetupKafka(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton(
            typeof(IKafkaMessageHandler<Contract.BusinessEvents.Sample.ISampleEvent>),
            typeof(OrleansKafkaAdapter<Contract.BusinessEvents.Sample.ISampleEvent, ISampleEvent, SampleEventKafkaMetadata>));

        services.AddSingleton(
            typeof(IKafkaMessageHandler<Contract.BusinessEvents.Sample2.ISample2Event>),
            typeof(OrleansKafkaAdapter<Contract.BusinessEvents.Sample2.ISample2Event, ISample2Event, Sample2EventKafkaMetadata>));

        services.AddKafkaGrainRouter<
            ISampleEvent,
            SampleEventKafkaMetadata,
            SampleView,
            KafkaGrainHandler<ISampleEvent, SampleView>>(
            ev => ev.SampleId.ToString())
            .AddDupChecker<SampleEventKafkaMetadata>()
            .AddMetadataUpdater<SampleEventKafkaMetadata>()
            .AddFaultedStateReset<SampleEventKafkaMetadata>();

        services.AddKafkaGrainRouter<
            ISample2Event,
            Sample2EventKafkaMetadata,
            Sample2View,
            KafkaGrainHandler<ISample2Event, Sample2View>>(
            ev => ev.Sample2Id.ToString())
            .AddDupChecker<Sample2EventKafkaMetadata>()
            .AddMetadataUpdater<Sample2EventKafkaMetadata>()
            .AddFaultedStateReset<Sample2EventKafkaMetadata>();

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

        services.ConfigTopicConsumer<Contract.BusinessEvents.Sample2.ISample2Event>(
            kafkaSettings.BootstrapServers,
            kafkaSettings.Sample2BusinessEventsTopic,
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

        services.ConfigTopicProducer<Contract.BusinessEvents.Sample2.Sample2EventFailure>(
            kafkaSettings.Sample2FailureEventsTopic,
            SubjectNameStrategy.Topic,
            SerializerType.JSON);

        services.ConfigTopicProducer<SampleThinEvent>(
            kafkaSettings.SampleThinEventsTopic,
            SubjectNameStrategy.Record,
            SerializerType.AVRO);

        services.ConfigTopicProducer<Sample2ThinEvent>(
            kafkaSettings.Sample2ThinEventsTopic,
            SubjectNameStrategy.Record,
            SerializerType.AVRO);

        services.ConfigTopicProducer<SampleFullRecord>(
            kafkaSettings.SampleFullRecordsTopic,
            SubjectNameStrategy.Topic,
            SerializerType.PROTO);

        services.ConfigTopicProducer<Sample2FullRecord>(
            kafkaSettings.Sample2FullRecordsTopic,
            SubjectNameStrategy.Topic,
            SerializerType.PROTO);

        #endregion

        return services;
    }
}
