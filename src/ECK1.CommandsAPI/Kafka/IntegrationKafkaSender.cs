using AutoMapper;
using ECK1.CommandsAPI.Domain.Samples;
using ECK1.CommonUtils.Mapping;
using ECK1.Kafka;
using MediatR;
using EventContracts = ECK1.Contracts.Kafka.BusinessEvents.Sample;

namespace ECK1.CommandsAPI.Kafka;

public record EventNotification<TEvent>(TEvent Event, int Version) : INotification;

public class SampleIntegrationKafkaSender(IMapper mapper, IKafkaTopicProducer<EventContracts.ISampleEvent> producer) :
    IntegrationBase<ISampleEvent, EventContracts.ISampleEvent>(mapper, producer)
{
    protected override string Key(EventContracts.ISampleEvent @event) => @event.SampleId.ToString();

    protected override EventContracts.ISampleEvent AdjustMessage(EventNotification<ISampleEvent> notification, EventContracts.ISampleEvent message)
    {
        message.Version = notification.Version;

        return message;
    }
}

public abstract class IntegrationBase<TDomainEvent, TContractEvent>(IMapper mapper, IKafkaTopicProducer<TContractEvent> producer) :
    MappingByNameBootstrapper<TDomainEvent, TContractEvent>, 
    INotificationHandler<EventNotification<TDomainEvent>>
    where TContractEvent : class
{
    protected abstract string Key(TContractEvent @event);

    protected abstract TContractEvent AdjustMessage(EventNotification<TDomainEvent> notification, TContractEvent message);

    public async Task Handle(EventNotification<TDomainEvent> notification, CancellationToken ct)
    {
        var domainEventType = notification.Event.GetType();
        var contractType = GetDestinationType(domainEventType);

        var eventKafkaMessage = mapper.Map(notification.Event, domainEventType, contractType) as TContractEvent
            ?? throw new InvalidOperationException("Couldnt cast event to contract type");

        await SendAsync(AdjustMessage(notification, eventKafkaMessage), ct);
    }

    public Task SendAsync(TContractEvent @event, CancellationToken ct) => producer.ProduceAsync(@event, Key(@event), ct);
}
