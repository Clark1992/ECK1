using ECK1.CommandsAPI.Domain.Shared;
using System.Text.Json.Serialization;

namespace ECK1.CommandsAPI.Domain.Sample2s;

// Required for serializing to DB
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Sample2CreatedEvent), nameof(Sample2CreatedEvent))]
[JsonDerivedType(typeof(Sample2CustomerEmailChangedEvent), nameof(Sample2CustomerEmailChangedEvent))]
[JsonDerivedType(typeof(Sample2ShippingAddressChangedEvent), nameof(Sample2ShippingAddressChangedEvent))]
[JsonDerivedType(typeof(Sample2LineItemAddedEvent), nameof(Sample2LineItemAddedEvent))]
[JsonDerivedType(typeof(Sample2LineItemRemovedEvent), nameof(Sample2LineItemRemovedEvent))]
[JsonDerivedType(typeof(Sample2StatusChangedEvent), nameof(Sample2StatusChangedEvent))]
[JsonDerivedType(typeof(Sample2TagAddedEvent), nameof(Sample2TagAddedEvent))]
[JsonDerivedType(typeof(Sample2TagRemovedEvent), nameof(Sample2TagRemovedEvent))]
//[JsonDerivedType(typeof(Sample2RebuiltEvent), nameof(Sample2RebuiltEvent))]
public interface ISample2Event : IDomainEvent
{
    Guid Sample2Id { get; }
}

public record Sample2Event(Guid Sample2Id) : ISample2Event
{
        [JsonIgnore]
        public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

        public Guid EventId { get; set; } = Guid.NewGuid();

        public int Version { get; set; }
}

public record Sample2CreatedEvent(
    Guid Sample2Id,
    Sample2Customer Customer,
    Address ShippingAddress,
    List<Sample2LineItem> LineItems,
    List<string> Tags,
    Sample2Status Status) : Sample2Event(Sample2Id);

public record Sample2CustomerEmailChangedEvent(Guid Sample2Id, string NewEmail) : Sample2Event(Sample2Id);
public record Sample2ShippingAddressChangedEvent(Guid Sample2Id, Address NewAddress) : Sample2Event(Sample2Id);
public record Sample2LineItemAddedEvent(Guid Sample2Id, Sample2LineItem Item) : Sample2Event(Sample2Id);
public record Sample2LineItemRemovedEvent(Guid Sample2Id, Guid ItemId) : Sample2Event(Sample2Id);
public record Sample2StatusChangedEvent(Guid Sample2Id, Sample2Status NewStatus, string Reason) : Sample2Event(Sample2Id);
public record Sample2TagAddedEvent(Guid Sample2Id, string Tag) : Sample2Event(Sample2Id);
public record Sample2TagRemovedEvent(Guid Sample2Id, string Tag) : Sample2Event(Sample2Id);
//public record Sample2RebuiltEvent(
//    Guid Sample2Id,
//    Sample2Customer Customer,
//    Address ShippingAddress,
//    List<Sample2LineItem> LineItems,
//    List<string> Tags,
//    Sample2Status Status) : Sample2Event(Sample2Id);
