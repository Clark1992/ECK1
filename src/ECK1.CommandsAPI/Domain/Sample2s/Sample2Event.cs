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
[JsonDerivedType(typeof(Sample2RebuiltEvent), nameof(Sample2RebuiltEvent))]
public interface ISample2Event
{
    Guid Sample2Id { get; }
    Guid EventId { get; }
    DateTimeOffset OccurredAt { get; set; }
}

public record Sample2Event(Guid Sample2Id) : ISample2Event
{
    [JsonIgnore]
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid EventId { get; set; } = Guid.NewGuid();
}

public enum Sample2Status
{
    Draft = 0,
    Submitted = 1,
    Paid = 2,
    Shipped = 3,
    Cancelled = 4,
}

public class Sample2Customer
{
    public Guid CustomerId { get; set; }
    public string Email { get; set; }
    public string Segment { get; set; }
}

public class Sample2Address
{
    public Guid Id { get; set; }
    public string Street { get; set; }
    public string City { get; set; }
    public string Country { get; set; }
}

public class Sample2Money
{
    public decimal Amount { get; set; }
    public string Currency { get; set; }
}

public class Sample2LineItem
{
    public Guid ItemId { get; set; }
    public string Sku { get; set; }
    public int Quantity { get; set; }
    public Sample2Money UnitPrice { get; set; }
}

public record Sample2CreatedEvent(
    Guid Sample2Id,
    Sample2Customer Customer,
    Sample2Address ShippingAddress,
    List<Sample2LineItem> LineItems,
    List<string> Tags,
    Sample2Status Status) : Sample2Event(Sample2Id);

public record Sample2CustomerEmailChangedEvent(Guid Sample2Id, string NewEmail) : Sample2Event(Sample2Id);
public record Sample2ShippingAddressChangedEvent(Guid Sample2Id, Sample2Address NewAddress) : Sample2Event(Sample2Id);
public record Sample2LineItemAddedEvent(Guid Sample2Id, Sample2LineItem Item) : Sample2Event(Sample2Id);
public record Sample2LineItemRemovedEvent(Guid Sample2Id, Guid ItemId) : Sample2Event(Sample2Id);
public record Sample2StatusChangedEvent(Guid Sample2Id, Sample2Status NewStatus, string Reason) : Sample2Event(Sample2Id);
public record Sample2TagAddedEvent(Guid Sample2Id, string Tag) : Sample2Event(Sample2Id);
public record Sample2TagRemovedEvent(Guid Sample2Id, string Tag) : Sample2Event(Sample2Id);
public record Sample2RebuiltEvent(
    Guid Sample2Id,
    Sample2Customer Customer,
    Sample2Address ShippingAddress,
    List<Sample2LineItem> LineItems,
    List<string> Tags,
    Sample2Status Status) : Sample2Event(Sample2Id);
