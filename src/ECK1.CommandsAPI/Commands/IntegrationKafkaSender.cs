using AutoMapper;
using ECK1.CommandsAPI.Domain.Samples;
using ECK1.CommonUtils.Mapping;
using ECK1.Kafka;
using MediatR;
using static ECK1.CommonUtils.Mapping.TypeUtils;
using EventContracts = ECK1.Contracts.Kafka.BusinessEvents.Sample;

namespace ECK1.CommandsAPI.Commands;

public record EventNotification<TEvent>(TEvent Event) : INotification;

public class SampleIntegrationKafkaSender(IMapper mapper, IKafkaTopicProducer<EventContracts.ISampleEvent> producer) :
    IntegrationBase<ISampleEvent, EventContracts.ISampleEvent>(mapper, producer)
{
    protected override string Key(EventContracts.ISampleEvent @event) => @event.SampleId.ToString();
}

public abstract class IntegrationBase<TDomainEvent, TContractEvent> :
    MappingByNameBootstrapper<TDomainEvent, TContractEvent>, 
    INotificationHandler<EventNotification<TDomainEvent>>
    where TContractEvent : class
{
    private readonly IKafkaTopicProducer<TContractEvent> producer;
    private readonly IMapper mapper;

    protected IntegrationBase(IMapper mapper, IKafkaTopicProducer<TContractEvent> producer)
    {
        this.mapper = mapper;
        this.producer = producer;
    }

    protected abstract string Key(TContractEvent @event);

    public async Task Handle(EventNotification<TDomainEvent> notification, CancellationToken ct)
    {
        var domainEventType = notification.Event.GetType();
        if (eventMapping.TryGetValue(domainEventType, out var contractType))
        {
            var eventKafkaMessage = mapper.Map(notification.Event, domainEventType, GetDestinationType(domainEventType)) as TContractEvent
                ?? throw new InvalidOperationException("Couldnt cast event to contract type");

            await SendAsync(eventKafkaMessage, ct);
        }
        else
        {
            throw new InvalidOperationException(
                $"No Event mapping found for {domainEventType.Name}");
        }
    }

    public Task SendAsync(TContractEvent @event, CancellationToken ct) => producer.ProduceAsync(@event, Key(@event), ct);
}
