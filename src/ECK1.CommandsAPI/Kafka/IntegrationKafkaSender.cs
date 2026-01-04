using AutoMapper;
using ECK1.CommandsAPI.Domain.Sample2s;
using ECK1.CommandsAPI.Domain.Samples;
using ECK1.CommonUtils.Mapping;
using ECK1.Kafka;
using MediatR;
using SampleContracts = ECK1.Contracts.Kafka.BusinessEvents.Sample;
using Sample2Contracts = ECK1.Contracts.Kafka.BusinessEvents.Sample2;

namespace ECK1.CommandsAPI.Kafka;

public record EventNotification<TEvent>(TEvent Event, int Version) : INotification;

public class SampleIntegrationKafkaSender(IMapper mapper, IKafkaTopicProducer<SampleContracts.ISampleEvent> producer) :
    IntegrationBase<ISampleEvent, SampleContracts.ISampleEvent>(mapper, producer)
{
    protected override string Key(SampleContracts.ISampleEvent @event) => @event.SampleId.ToString();

    protected override SampleContracts.ISampleEvent AdjustMessage(EventNotification<ISampleEvent> notification, SampleContracts.ISampleEvent message)
    {
        message.Version = notification.Version;

        return message;
    }
}

public class Sample2IntegrationKafkaSender(IMapper mapper, IKafkaTopicProducer<Sample2Contracts.ISample2Event> producer) :
    IntegrationBase<ISample2Event, Sample2Contracts.ISample2Event>(mapper, producer)
{
    protected override string Key(Sample2Contracts.ISample2Event @event) => @event.Sample2Id.ToString();

    protected override Sample2Contracts.ISample2Event AdjustMessage(EventNotification<ISample2Event> notification, Sample2Contracts.ISample2Event message)
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
