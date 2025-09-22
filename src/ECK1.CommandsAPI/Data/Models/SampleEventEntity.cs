using ECK1.CommandsAPI.Domain.Samples;
using System.Text.Json;

namespace ECK1.CommandsAPI.Data.Models;
public class SampleEventEntity
{
    public Guid EventId { get; set; }
    public Guid SampleId { get; set; }
    public string EventType { get; set; } = default!;
    public string EventData { get; set; } = default!;
    public DateTimeOffset OccurredAt { get; set; }
    public int Version { get; set; }

    public ISampleEvent ToDomainEvent()
    {
        var @event = JsonSerializer.Deserialize<ISampleEvent>(EventData)!;
        @event.OccurredAt = OccurredAt;

        return @event;
    }

    public static SampleEventEntity FromDomainEvent(ISampleEvent ev, int version) => new()
    {
        EventId = Guid.NewGuid(),
        SampleId = ev.SampleId,
        EventType = ev.GetType().Name,
        EventData = JsonSerializer.Serialize(ev),
        OccurredAt = ev.OccurredAt,
        Version = version
    };
}
