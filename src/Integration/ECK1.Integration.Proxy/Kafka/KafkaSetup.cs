using Confluent.Kafka;
using Confluent.SchemaRegistry;
using ECK1.Contracts.Kafka.BusinessEvents;
using ECK1.Integration.Common;
using ECK1.Integration.EntityStore.Configuration.Generated;
using ECK1.Integration.Plugin.Abstractions;
using ECK1.Kafka.Extensions;

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

        services.ConfigTopicProducer<EventFailure>(
            kafkaSettings.FailureEventsTopic,
            SubjectNameStrategy.Topic,
            SerializerType.JSON);

        #region Event consumers
        var cacheSettings = config
            .GetSection(CacheServiceSettings.Section)
            .Get<CacheServiceSettings>() ?? new CacheServiceSettings();

        var eventConfig = new EventConsumerConfig(
            integrationConfig,
            plugin,
            services,
            kafkaSettings,
            cacheSettings);

        ClientConsumerConfigurator.AddEventConsumers(
            cacheSettings.ShortTerm.Url,
            cacheSettings.LongTerm.Url,
            eventConfig);
        #endregion

        return services;
    }
}


