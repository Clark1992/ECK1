using System.Text.Json.Serialization;
using ECK1.Contracts.Shared;

namespace ECK1.Contracts.Kafka.BusinessEvents.Sample2;

[Newtonsoft.Json.JsonConverter(typeof(Polymorph<ISample2Event>), "$type")]
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
    DateTimeOffset OccurredAt { get; }
    int Version { get; set; }
}

public abstract class Sample2Event : ISample2Event
{
    public Guid Sample2Id { get; set; }
    public Guid EventId { get; set; }
    public int Version { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}

public enum Sample2Status
{
    Draft = 0,
    Submitted = 1,
    Paid = 2,
    Shipped = 3,
    Cancelled = 4,
}

public class Sample2CreatedEvent : Sample2Event
{
    public Sample2Customer Customer { get; set; }
    public Sample2Address ShippingAddress { get; set; }
    public List<Sample2LineItem> LineItems { get; set; }
    public List<string> Tags { get; set; }
    public Sample2Status Status { get; set; }
}

public class Sample2CustomerEmailChangedEvent : Sample2Event
{
    public string NewEmail { get; set; }
}

public class Sample2ShippingAddressChangedEvent : Sample2Event
{
    public Sample2Address NewAddress { get; set; }
}

public class Sample2LineItemAddedEvent : Sample2Event
{
    public Sample2LineItem Item { get; set; }
}

public class Sample2LineItemRemovedEvent : Sample2Event
{
    public Guid ItemId { get; set; }
}

public class Sample2StatusChangedEvent : Sample2Event
{
    public Sample2Status NewStatus { get; set; }
    public string Reason { get; set; }
}

public class Sample2TagAddedEvent : Sample2Event
{
    public string Tag { get; set; }
}

public class Sample2TagRemovedEvent : Sample2Event
{
    public string Tag { get; set; }
}

public class Sample2RebuiltEvent : Sample2Event
{
    public Sample2Customer Customer { get; set; }
    public Sample2Address ShippingAddress { get; set; }
    public List<Sample2LineItem> LineItems { get; set; }
    public List<string> Tags { get; set; }
    public Sample2Status Status { get; set; }
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

public class Sample2EventFailure
{
    public Guid Sample2Id { get; set; }

    public string FailedEventType { get; set; }

    public DateTimeOffset FailureOccurredAt { get; set; } = DateTimeOffset.UtcNow;

    public string ErrorMessage { get; set; }

    public string StackTrace { get; set; }
}
