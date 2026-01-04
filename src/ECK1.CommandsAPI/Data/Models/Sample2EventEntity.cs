using ECK1.CommandsAPI.Domain.Sample2s;
using System.Text.Json;

namespace ECK1.CommandsAPI.Data.Models;

public class Sample2EventEntity
{
    public Guid EventId { get; set; }
    public Guid Sample2Id { get; set; }
    public string EventType { get; set; } = default!;
    public string EventData { get; set; } = default!;
    public DateTimeOffset OccurredAt { get; set; }
    public int Version { get; set; }

    public ISample2Event ToDomainEvent()
    {
        var @event = JsonSerializer.Deserialize<ISample2Event>(EventData)!;
        @event.OccurredAt = OccurredAt;

        return @event;
    }

    public static Sample2EventEntity FromDomainEvent(ISample2Event ev, int version) => new()
    {
        EventId = ev.EventId,
        Sample2Id = ev.Sample2Id,
        EventType = ev.GetType().Name,
        EventData = JsonSerializer.Serialize(ev),
        OccurredAt = ev.OccurredAt,
        Version = version
    };
}
