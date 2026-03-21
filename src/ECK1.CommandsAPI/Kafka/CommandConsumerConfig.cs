using Confluent.SchemaRegistry;
using ECK1.AsyncApi.Generated;
using ECK1.CommandsAPI.Commands;
using ECK1.CommandsAPI.Kafka.Orleans;
using ECK1.Kafka;
using ECK1.Kafka.Extensions;
using ECK1.Orleans.Grains;

namespace ECK1.CommandsAPI.Kafka;

public class CommandConsumerConfig(
    IServiceCollection services,
    IConfiguration config,
    KafkaSettings kafkaSettings) : AbstractCommandConfigurator
{
    public override void RegisterCommand<TCmd>(string topicConfigKey, string topic)
    {
        var resolvedTopic = !string.IsNullOrEmpty(topic)
            ? topic
            : config[topicConfigKey]
              ?? throw new InvalidOperationException($"Missing topic config for '{topicConfigKey}'.");

        services.AddSingleton(
            typeof(IKafkaMessageHandler<TCmd>),
            typeof(OrleansAdapter<TCmd, NullGrainMetadata, ICommandResult>));

        services.ConfigTopicConsumer<TCmd>(
            kafkaSettings.BootstrapServers,
            resolvedTopic,
            kafkaSettings.GroupId,
            SubjectNameStrategy.Record,
            SerializerType.JSON,
            c => c.WithAuth(kafkaSettings.User, kafkaSettings.Secret));
    }
}
