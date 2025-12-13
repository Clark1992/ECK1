using Confluent.Kafka;
using ECK1.Integration.Plugin.Abstractions;
using ECK1.Kafka.Extensions;
using ECK1.Integration.EntityStore.Configuration.Generated;

namespace ECK1.Integration.Proxy.Kafka;

public static class KafkaSetup
{
    public static IServiceCollection SetupKafka(
        this IServiceCollection services,
        IConfiguration config,
        IntegrationConfig integrationConfig,
        string plugin)
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

        #region Consume progress tracking

        //services.ConfigTopicProducer<ProgressStatusRecord>(
        //    kafkaSettings.CacheProgressTopic,
        //    SubjectNameStrategy.Topic,
        //    SerializerType.JSON);

        #endregion

        #region Event consumers
        var eventConfig = new EventConsumerConfig(
            integrationConfig,
            plugin,
            services,
            kafkaSettings);

        var grpcUrl = config.GetValue<string>("CacheService:ShortTerm:Url");
        ClientConsumerConfigurator.AddEventConsumers(grpcUrl, eventConfig);
        #endregion

        return services;
    }
}


