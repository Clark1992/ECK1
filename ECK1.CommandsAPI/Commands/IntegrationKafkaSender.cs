using AutoMapper;
using ECK1.CommandsAPI.Domain.Samples;
using MediatR;

using EventContracts = ECK1.Contracts.BusinessEvents.Sample;
using static ECK1.CommandsAPI.Utils.TypeUtils;

namespace ECK1.CommandsAPI.Commands;

public record EventNotification<TEvent>(TEvent Event) : INotification;

public class IntegrationKafkaSender(IMapper mapper) : IntegrationBase<ISampleEvent, EventContracts.ISampleEvent>(mapper)
{ }

public abstract class IntegrationBase<TDomainEvent, TContractEvent>(IMapper mapper) : 
    IntegrationBootstrapper<TDomainEvent, TContractEvent>, 
    INotificationHandler<EventNotification<TDomainEvent>>
{
    public async override Task Handle(EventNotification<TDomainEvent> notification, CancellationToken ct)
    {
        var domainEventType = notification.Event.GetType();
        if (eventMapping.TryGetValue(domainEventType, out var contractType))
        {
            var eventKafkaMessage = mapper.Map(notification.Event, domainEventType, contractType);
            // send to kafka
        }
        else
        {
            throw new InvalidOperationException(
                $"No Event mapping found for {domainEventType.Name}");
        }
    }
}

public abstract class IntegrationBootstrapper<TDomainEvent, TContractEvent>
{
    protected static readonly Dictionary<Type, Type> eventMapping
        = new();

    static IntegrationBootstrapper()
    {
        eventMapping = GetEventTypeMapping<TDomainEvent, TContractEvent>();
    }

    public abstract Task Handle(EventNotification<TDomainEvent> @event, CancellationToken ct);
}
