using Orleans;

namespace ECK1.ViewProjector.Events;

public interface ISample2Event
{
    Guid Sample2Id { get; }
    Guid EventId { get; }
    DateTimeOffset OccurredAt { get; }
    int Version { get; }
}

[GenerateSerializer]
public record Sample2Event(Guid Sample2Id, int Version) : ISample2Event
{
    [Id(0)]
    public DateTimeOffset OccurredAt { get; set; }

    [Id(1)]
    public Guid EventId { get; set; }
}

[GenerateSerializer]
public record Sample2CreatedEvent(Guid Sample2Id, Sample2Customer Customer, Sample2Address ShippingAddress, List<Sample2LineItem> LineItems, List<string> Tags, Sample2Status Status, int Version) : Sample2Event(Sample2Id, Version);
[GenerateSerializer]
public record Sample2CustomerEmailChangedEvent(Guid Sample2Id, string NewEmail, int Version) : Sample2Event(Sample2Id, Version);
[GenerateSerializer]
public record Sample2ShippingAddressChangedEvent(Guid Sample2Id, Sample2Address NewAddress, int Version) : Sample2Event(Sample2Id, Version);
[GenerateSerializer]
public record Sample2LineItemAddedEvent(Guid Sample2Id, Sample2LineItem Item, int Version) : Sample2Event(Sample2Id, Version);
[GenerateSerializer]
public record Sample2LineItemRemovedEvent(Guid Sample2Id, Guid ItemId, int Version) : Sample2Event(Sample2Id, Version);
[GenerateSerializer]
public record Sample2StatusChangedEvent(Guid Sample2Id, Sample2Status NewStatus, string Reason, int Version) : Sample2Event(Sample2Id, Version);
[GenerateSerializer]
public record Sample2TagAddedEvent(Guid Sample2Id, string Tag, int Version) : Sample2Event(Sample2Id, Version);
[GenerateSerializer]
public record Sample2TagRemovedEvent(Guid Sample2Id, string Tag, int Version) : Sample2Event(Sample2Id, Version);
[GenerateSerializer]
public record Sample2RebuiltEvent(Guid Sample2Id, Sample2Customer Customer, Sample2Address ShippingAddress, List<Sample2LineItem> LineItems, List<string> Tags, Sample2Status Status, int Version) : Sample2Event(Sample2Id, Version);

[GenerateSerializer]
public enum Sample2Status
{
    [Id(0)] Draft = 0,
    [Id(1)] Submitted = 1,
    [Id(2)] Paid = 2,
    [Id(3)] Shipped = 3,
    [Id(4)] Cancelled = 4,
}

[GenerateSerializer]
public class Sample2Customer
{
    [Id(0)] public Guid CustomerId { get; set; }
    [Id(1)] public string Email { get; set; }
    [Id(2)] public string Segment { get; set; }
}

[GenerateSerializer]
public class Sample2Address
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public string Street { get; set; }
    [Id(2)] public string City { get; set; }
    [Id(3)] public string Country { get; set; }
}

[GenerateSerializer]
public class Sample2Money
{
    [Id(0)] public decimal Amount { get; set; }
    [Id(1)] public string Currency { get; set; }
}

[GenerateSerializer]
public class Sample2LineItem
{
    [Id(0)] public Guid ItemId { get; set; }
    [Id(1)] public string Sku { get; set; }
    [Id(2)] public int Quantity { get; set; }
    [Id(3)] public Sample2Money UnitPrice { get; set; }
}
