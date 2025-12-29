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
#if DEBUG
                $"{kafkaSettings.GroupIdPrefix}-{plugin}-{Guid.NewGuid()}",
#else
                $"{kafkaSettings.GroupIdPrefix}-{plugin}",
#endif
                SubjectNameStrategy.Record,
                SerializerType.AVRO,
                sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<EventConsumerConfig>>();
                    var plugin = sp.GetRequiredService<IIntergationPlugin<ThinEvent, TMessage>>();
                    int? fieldMaskHash = null;

                    return async (key, @event, _, ct) =>
                    {
                        var occuredAt = @event.OccuredAt;
                        logger.LogInformation("{Topic}: Start handle '{message}:{id} (OccureAt: {occuredAt})'", topic, @event.EventType, @event.EntityId, occuredAt);

                        if (@event.EntityId == default)
                            return;

                        // TODO: dont send mask every time
                        var resp = await getter(new GetEntityRequest<TMessage>
                        {
                            FieldMaskHash = fieldMaskHash,
                            Id = @event.EntityId.ToString(),
                            Mask = mask,
                            MinVersion = @event.Version
                        });

                        // TODO: error handling if mask hash cache missed => resend with full mask
                        if (resp.Item is null)
                        {
                            throw new Exception("Response from cache is null");
                        }

                        // TODO: general error handling

                        fieldMaskHash = resp.FieldMaskHash;

                        await plugin.PushAsync(@event, resp.Item);

                        logger.LogInformation("{Topic}: Handled '{message}:{id}'", topic, @event.EventType, @event.EntityId);
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
