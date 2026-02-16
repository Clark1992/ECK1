using Confluent.Kafka;
using Confluent.SchemaRegistry;
using ECK1.Contracts.Kafka.BusinessEvents;
using ECK1.Contracts.Kafka.BusinessEvents.Sample2;
using ECK1.Kafka;
using ECK1.Kafka.Extensions;

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

        services.ConfigSimpleTopicProducer<Guid>();

        return services;
    }
}