using Confluent.Kafka;
using Confluent.SchemaRegistry;
using ECK1.Integration.Common;
using ECK1.Integration.EntityStore.CommonDto.Generated;
using ECK1.Integration.EntityStore.Configuration.Generated;
using ECK1.Integration.Plugin.Abstractions;
using ECK1.IntegrationContracts.Kafka.IntegrationRecords.Generated;
using ECK1.Kafka.Extensions;
using ECK1.IntegrationContracts.Abstractions;
using Grpc.Core;
using ECK1.Kafka;
using Polly;
using Polly.Retry;
using ECK1.Contracts.Kafka.BusinessEvents;

namespace ECK1.Integration.Proxy.Kafka;

public class EventConsumerConfig(
    IntegrationConfig config,
    string plugin,
    IServiceCollection services,
    KafkaSettings kafkaSettings,
    CacheServiceSettings cacheSettings) : AbstractClientConsumerConfigurator
{
    public override void SetupConsumer<TMessage>(
        Func<GetEntityRequest<TMessage>, ValueTask<EntityResponse<TMessage>>> shortTermGetter,
        Func<GetEntityRequest<TMessage>, ValueTask<EntityResponse<TMessage>>> longTermGetter)
    {
        if (!config.TryGetValue(typeof(TMessage).FullName, out var entry))
            return;

        var topic = entry.EventsTopic;
        var fields = entry.PluginConfig.GetSection("Fields").Get<List<string>>();
        var mask = new FieldMask { Paths = fields };

        var threshold = GetStaleThreshold(cacheSettings);

        services.ConfigTopicConsumer<ThinEvent>(
                kafkaSettings.BootstrapServers,
                topic,
#if DEBUG
                //$"{kafkaSettings.GroupIdPrefix}-{plugin}-{Guid.NewGuid()}",
                $"{kafkaSettings.GroupIdPrefix}-{plugin}",
#else
                $"{kafkaSettings.GroupIdPrefix}-{plugin}",
#endif
                SubjectNameStrategy.Record,
                SerializerType.AVRO,
                sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<EventConsumerConfig>>();
                    var plugin = sp.GetRequiredService<IIntergationPlugin<ThinEvent, TMessage>>();
                    var producer = sp.GetRequiredService<IKafkaTopicProducer<EventFailure>>();
                    int? fieldMaskHash = null;

                    return async (key, @event, _, ct) =>
                    {
                        var occuredAt = @event.OccuredAt;
                        logger.LogInformation("{Topic}: Start handle '{message}:{id} (OccureAt: {occuredAt})'", topic, @event.EventType, @event.EntityId, occuredAt);

                        if (@event.EntityId == default)
                            return;

                        // TODO: dont send mask every time
                        var request = new GetEntityRequest<TMessage>
                        {
                            FieldMaskHash = fieldMaskHash,
                            Id = @event.EntityId.ToString(),
                            Mask = mask,
                            MinVersion = @event.Version
                        };

                        var useLongTerm = ShouldUseLongTerm(occuredAt, threshold);

                        try
                        {
                            EntityResponse<TMessage> response;

                            if (useLongTerm)
                            {
                                logger.LogInformation("{Topic}: Using long-term cache (stale by {Minutes} minutes)", topic, threshold.TotalMinutes);
                                response = await GetFromLongTermOrThrow(longTermGetter, request, logger);
                            }
                            else
                            {
                                response = await GetFromShortTermWithRetry(shortTermGetter, request, logger, cacheSettings, ct);

                                if (response is null)
                                {
                                    logger.LogInformation("{Topic}: Short-term cache miss. Falling back to long-term cache", topic);
                                    response = await GetFromLongTermOrThrow(longTermGetter, request, logger);
                                }
                            }

                            fieldMaskHash = response.FieldMaskHash;

                            await plugin.PushAsync(@event, response.Item);

                            logger.LogInformation("{Topic}: Handled '{message}:{id}'", topic, @event.EventType, @event.EntityId);
                        }
                        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
                        {
                            var reason = useLongTerm
                                ? $"[{plugin}] Entity not found in long-term cache for stale event"
                                : $"[{plugin}] Entity not found in short-term and long-term caches";

                            await PublishToDlqAsync(
                                producer,
                                @event,
                                entry.EntityType,
                                reason,
                                logger,
                                ct);
                        }
                    };
                },
                c =>
                {
                    c.WithAuth(kafkaSettings.User, kafkaSettings.Secret);
                    c.AutoOffsetReset = AutoOffsetReset.Earliest;
                    c.EnableAutoCommit = false;
                });
    }

    private static async Task PublishToDlqAsync(
        IKafkaTopicProducer<EventFailure> producer,
        ThinEvent @event,
        string entityType,
        string reason,
        ILogger logger,
        CancellationToken ct)
    {
        var payload = new EventFailure
        {
            EntityId = @event.EntityId,
            EntityType = entityType,
            ErrorMessage = reason,
            FailedEventType = @event.EventType,
        };

        await producer.ProduceAsync(payload, @event.EntityId.ToString(), ct);

        logger.LogWarning(
            "Failure: Event '{message}:{id}' sent to DLQ topic. Reason: {Reason}",
            @event.EventType,
            @event.EntityId,
            reason);
    }

    private static TimeSpan GetStaleThreshold(CacheServiceSettings settings)
    {
        if (settings?.StaleEventThresholdSeconds > 0)
        {
            return TimeSpan.FromSeconds(settings.StaleEventThresholdSeconds);
        }

        return TimeSpan.Zero;
    }

    private static bool ShouldUseLongTerm(DateTime occuredAt, TimeSpan threshold)
    {
        if (threshold <= TimeSpan.Zero)
        {
            return true;
        }

        return DateTime.UtcNow - occuredAt > threshold;
    }

    private static async ValueTask<EntityResponse<TMessage>> GetFromShortTermWithRetry<TMessage>(
        Func<GetEntityRequest<TMessage>, ValueTask<EntityResponse<TMessage>>> getter,
        GetEntityRequest<TMessage> request,
        ILogger logger,
        CacheServiceSettings cacheSettings,
        CancellationToken ct)
        where TMessage : class, IIntegrationMessage
    {
        var maxAttempts = Math.Max(1, cacheSettings?.Retry?.MaxAttempts ?? 1);
        var delayMs = Math.Max(0, cacheSettings?.Retry?.DelayMs ?? 0);

        AsyncRetryPolicy<EntityResponse<TMessage>> policy = Policy<EntityResponse<TMessage>>
            .Handle<RpcException>(ex => ex.StatusCode == StatusCode.NotFound)
            .OrResult(resp => resp?.Item == null)
            .WaitAndRetryAsync(
                maxAttempts,
                _ => TimeSpan.FromMilliseconds(delayMs),
                (outcome, timespan, attempt, _) =>
                {
                    logger.LogWarning(
                        "Short-term cache miss for {Id}. Retry {Attempt}/{MaxAttempts} after {Delay}ms",
                        request.Id,
                        attempt,
                        maxAttempts,
                        timespan.TotalMilliseconds);
                    return Task.CompletedTask;
                });

        var response = await policy.ExecuteAsync(
            ct => getter(request).AsTask(),
            ct);

        if (response?.Item == null)
        {
            logger.LogWarning("Short-term cache miss after {Attempts} attempts for {Id}", maxAttempts, request.Id);
            return null;
        }

        return response;
    }

    private static async ValueTask<EntityResponse<TMessage>> GetFromLongTermOrThrow<TMessage>(
        Func<GetEntityRequest<TMessage>, ValueTask<EntityResponse<TMessage>>> longTermGetter,
        GetEntityRequest<TMessage> request,
        ILogger logger)
        where TMessage : class, IIntegrationMessage
    {
        try
        {
            var response = await longTermGetter(request);

            if (response?.Item is null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Entity not found in long-term cache"));
            }

            return response;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            logger.LogWarning("Long-term cache miss for {Id}", request.Id);
            throw;
        }
    }
}
