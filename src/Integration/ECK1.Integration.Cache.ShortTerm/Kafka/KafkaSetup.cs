using Confluent.Kafka;
using ECK1.Integration.EntityStore.Configuration.Generated;
using ECK1.Integration.Plugin.Abstractions;
using ECK1.Kafka;
using ECK1.Kafka.Extensions;

namespace ECK1.Integration.Cache.ShortTerm.Kafka;

public static class KafkaSetup
{
    public static IServiceCollection SetupKafka(this IServiceCollection services, IntegrationConfig integrationConfig, IConfiguration config)
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

        #region Produce progress tracking

        //services.ConfigTopicProducer<ProgressStatusRecord>(
        //    kafkaSettings.CacheProgressTopic,
        //    SubjectNameStrategy.Topic,
        //    SerializerType.JSON);

        #endregion

        #region Cache

        services.AddSingleton(typeof(IKafkaMessageHandler<>), typeof(CachePopulator<>));

        var recordConsumerConfig = new RecordConsumerConfig(
            integrationConfig,
            services,
            kafkaSettings);

        ServerConsumerConfigurator.AddEventConsumers(recordConsumerConfig);

        #endregion

        return services;
    }
}
