using AutoMapper;
using ECK1.CommonUtils.Mapping;
using ECK1.Kafka;
using ECK1.Orleans;
using ECK1.Orleans.Kafka;
using ECK1.ReadProjector.Notifications;
using MediatR;

namespace ECK1.ReadProjector.Kafka.Orleans;

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

public class KafkaMessageHandler<TEvent, TState>(IMediator mediator) : IStatefulGrainHandler<TEvent, TState>
    where TEvent : class
{
    public Task<TState> Handle(TEvent ev, TState view, CancellationToken ct) =>
        mediator.Send(new EventWithStateNotification<TEvent, TState>(ev, view), ct);
}