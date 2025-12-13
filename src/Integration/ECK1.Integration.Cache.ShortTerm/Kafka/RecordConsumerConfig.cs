using Confluent.Kafka;
using Confluent.SchemaRegistry;
using ECK1.Integration.Plugin.Abstractions;
using ECK1.Kafka.Extensions;
using ECK1.Integration.EntityStore.Configuration.Generated;

namespace ECK1.Integration.Cache.ShortTerm.Kafka;

public class RecordConsumerConfig(
    IntegrationConfig config,
    IServiceCollection services,
    KafkaSettings kafkaSettings) : AbstractServerConsumerConfigurator
{
    public override void SetupConsumer<TMessage>()
    {
        if (!config.TryGetValue(typeof(TMessage).FullName, out var entry))
            return;

        var topic = entry.RecordTopic;

        services.ConfigTopicConsumer<TMessage>(
            kafkaSettings.BootstrapServers,
            topic,
            kafkaSettings.GroupId,
            SubjectNameStrategy.Topic,
            SerializerType.PROTO,
            c =>
            {
                c.WithAuth(kafkaSettings.User, kafkaSettings.Secret);
                c.AutoOffsetReset = AutoOffsetReset.Earliest;
                c.EnableAutoCommit = false;
            });
    }
}
