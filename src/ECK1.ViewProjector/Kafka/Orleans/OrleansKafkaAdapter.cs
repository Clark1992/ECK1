using AutoMapper;
using ECK1.CommonUtils.Mapping;
using ECK1.Kafka;
using ECK1.Orleans;
using ECK1.Orleans.Kafka;
using ECK1.ViewProjector.Notifications;
using MediatR;

namespace ECK1.ViewProjector.Kafka.Orleans;

public class OrleansKafkaAdapter<TValue, TOrleansSerializableValue, TMetadata> : 
    MappingByNameBootstrapper<TValue, TOrleansSerializableValue>, IKafkaMessageHandler<TValue>
    where TValue : class
    where TOrleansSerializableValue : class
{
    private readonly IKafkaGrainRouter<TOrleansSerializableValue, TMetadata> router;
    private readonly IMapper mapper;

    public OrleansKafkaAdapter(IKafkaGrainRouter<TOrleansSerializableValue, TMetadata> router, IMapper mapper)
    {
        this.router = router;
        this.mapper = mapper;
    }

    public async Task Handle(string key, TValue message, KafkaMessageId _, CancellationToken ct)
    {
        Type contractType = message.GetType();
        var orleansFriendlyValue = (TOrleansSerializableValue)mapper.Map(
            message,
            contractType,
            GetDestinationType(contractType));
        await router.RouteToGrain(orleansFriendlyValue, ct);
    }
}

public class KafkaMessageHandler<TEvent, TState>(IMediator mediator, ILogger<KafkaMessageHandler<TEvent, TState>> logger) 
    : IStatefulGrainHandler<TEvent, TState>
    where TEvent : class
{
    public async Task<TState> Handle(TEvent ev, TState view, CancellationToken ct)
    { 
        var type = ev.GetType();
        logger.LogInformation("Handling {messageType}", type);
        var newState = await mediator.Send(new EventWithStateNotification<TEvent, TState>(ev, view), ct);

        logger.LogInformation("Handled {messageType}", type);

        return newState;
    }
}