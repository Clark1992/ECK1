using Confluent.Kafka;
using Confluent.SchemaRegistry;
using ECK1.Integration.EntityStore.CommonDto.Generated;
using ECK1.Integration.EntityStore.Configuration.Generated;
using ECK1.Integration.Plugin.Abstractions;
using ECK1.IntegrationContracts.Kafka.IntegrationRecords.Generated;
using ECK1.Kafka.Extensions;

namespace ECK1.Integration.Proxy.Kafka;

public class EventConsumerConfig(
    IntegrationConfig config,
    string plugin,
    IServiceCollection services,
    KafkaSettings kafkaSettings) : AbstractClientConsumerConfigurator
{
    public override void SetupConsumer<TMessage>(
        Func<GetEntityRequest<TMessage>, ValueTask<EntityResponse<TMessage>>> getter)
    {
        if (!config.TryGetValue(typeof(TMessage).FullName, out var entry))
            return;

        var topic = entry.EventsTopic;
        var fields = entry.PluginConfig.GetSection("Fields").Get<List<string>>();
        var mask = new FieldMask { Paths = fields };

        services.ConfigTopicConsumer<ThinEvent>(
                kafkaSettings.BootstrapServers,
                topic,
                $"{kafkaSettings.GroupIdPrefix}-{plugin}",
                SubjectNameStrategy.Record,
                SerializerType.AVRO,
                sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<EventConsumerConfig>>();
                    var plugin = sp.GetRequiredService<IIntergationPlugin<TMessage>>();
                    int? fieldMaskHash = null;

                    return async (key, value, _, ct) =>
                    {
                        var occuredAt = value.OccuredAt;
                        logger.LogInformation("{Topic}: Start handle '{message}:{id} (OccureAt: {occuredAt})'", topic, value.EventType, value.Id, occuredAt);

                        if (!value.Id.HasValue)
                            return;

                        // TODO: dont send mask every time
                        var resp = await getter(new GetEntityRequest<TMessage>
                        {
                            FieldMaskHash = fieldMaskHash,
                            Id = value.Id.Value.ToString(),
                            Mask = mask,
                            MinVersion = value.Version
                        });

                        // TODO: error handling if mask hash cache missed => resend with full mask
                        if (resp.Item is null)
                        {
                            throw new Exception("Response from cache is null");
                        }

                        // TODO: general error handling

                        fieldMaskHash = resp.FieldMaskHash;

                        await plugin.PushAsync(resp.Item);

                        logger.LogInformation("{Topic}: Handled '{message}:{id}'", topic, value.EventType, value.Id);
                    };
                },
                c =>
                {
                    c.WithAuth(kafkaSettings.User, kafkaSettings.Secret);
                    c.AutoOffsetReset = AutoOffsetReset.Earliest;
                    c.EnableAutoCommit = false;
                });
    }
}
