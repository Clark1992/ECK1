using Confluent.Kafka;
using Confluent.SchemaRegistry;
using ECK1.Contracts.Kafka.BusinessEvents.Sample;
using ECK1.Kafka;
using ECK1.Kafka.Extensions;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.Contracts;

namespace ECK1.FailedViewRebuilder.Kafka;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection SetupKafka(this IServiceCollection services, IConfiguration config)
    {
        var kafkaSettings = config
            .GetSection(KafkaSettings.Section)
            .Get<KafkaSettings>();

        services.Configure<KafkaSettings>(config.GetSection(KafkaSettings.Section));

        services
            .WithSchemaRegistry(kafkaSettings.SchemaRegistryUrl,
                c => c.WithAuth(kafkaSettings.User, kafkaSettings.Secret));

        services.AddSingleton<IKafkaMessageHandler<SampleEventFailure>, IKafkaMessageHandler<SampleEventFailure>>();

        services.ConfigTopicConsumer<SampleEventFailure>(
            kafkaSettings.BootstrapServers,
            kafkaSettings.SampleFailureEventsTopic,
            kafkaSettings.GroupId,
            SubjectNameStrategy.Topic,
            SerializerType.JSON,
            c => c.WithAuth(kafkaSettings.User, kafkaSettings.Secret));

        services.AddHostedService<KafkaTopicConsumerService>();

        services
            .AddKafkaRootProducer(kafkaSettings.BootstrapServers,
            c =>
            {
                c.Acks = Acks.Leader;
                c.WithAuth(kafkaSettings.User, kafkaSettings.Secret);
                c.AllowAutoCreateTopics = true;
            });

        services.ConfigTopicProducer<SampleEventFailure>(
            kafkaSettings.SampleEventsRebuildRequestTopic,
            SubjectNameStrategy.Topic,
            SerializerType.JSON);

        return services;
    }
}