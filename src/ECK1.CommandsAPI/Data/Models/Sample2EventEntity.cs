using ECK1.CommandsAPI.Domain;
using ECK1.CommandsAPI.Domain.Sample2s;
using System.Text.Json;

namespace ECK1.CommandsAPI.Data.Models;

public class Sample2EventEntity : IEventEntity
{
    public Guid EventId { get; set; }
    public string EventType { get; set; } = default!;
    public string EventData { get; set; } = default!;
    public DateTimeOffset OccurredAt { get; set; }
    public int Version { get; set; }

    public Guid AggregateId { get; private set; }

    public IDomainEvent ToDomainEvent()
    {
        var @event = JsonSerializer.Deserialize<ISample2Event>(EventData)!;
        @event.OccurredAt = OccurredAt;

        return @event;
    }

    public static IEventEntity FromDomainEvent(IDomainEvent domainEvent)
    {
        if (domainEvent is not ISample2Event ev)
        {
            throw new InvalidOperationException($"Invalid event type '{domainEvent.GetType().Name}' for {nameof(Sample2EventEntity)}.");
        }

        return new Sample2EventEntity
        {
            EventId = ev.EventId,
            AggregateId = ev.Sample2Id,
            EventType = ev.GetType().Name,
            EventData = JsonSerializer.Serialize(ev),
            OccurredAt = ev.OccurredAt,
            Version = ev.Version
        };
    }
}
