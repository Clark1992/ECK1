using ECK1.IntegrationContracts.Abstractions;
using ProtoBuf;

namespace ECK1.IntegrationContracts.Kafka.IntegrationRecords.Sample2;

[ProtoContract]
public class Sample2FullRecord : IIntegrationMessage
{
    [ProtoIgnore] public string Id => Sample2Id.ToString();

    [ProtoMember(1)] public Guid Sample2Id { get; set; }
    [ProtoMember(2)] public Sample2Customer Customer { get; set; }
    [ProtoMember(3)] public Sample2Address ShippingAddress { get; set; }
    [ProtoMember(4)] public List<Sample2LineItem> LineItems { get; set; }
    [ProtoMember(5)] public List<Sample2Tag> Tags { get; set; }
    [ProtoMember(6)] public Sample2Status Status { get; set; }
    [ProtoMember(7)] public int Version { get; set; }
    [ProtoMember(8)] public DateTime OccuredAt { get; set; }
}

[ProtoContract]
public enum Sample2Status
{
    [ProtoEnum] Draft = 0,
    [ProtoEnum] Submitted = 1,
    [ProtoEnum] Paid = 2,
    [ProtoEnum] Shipped = 3,
    [ProtoEnum] Cancelled = 4,
}

[ProtoContract]
public class Sample2Customer
{
    [ProtoMember(1)] public Guid CustomerId { get; set; }
    [ProtoMember(2)] public string Email { get; set; }
    [ProtoMember(3)] public string Segment { get; set; }
}

[ProtoContract]
public class Sample2Address
{
    [ProtoMember(1)] public Guid Id { get; set; }
    [ProtoMember(2)] public string Street { get; set; }
    [ProtoMember(3)] public string City { get; set; }
    [ProtoMember(4)] public string Country { get; set; }
}

[ProtoContract]
public class Sample2Money
{
    [ProtoMember(1)] public decimal Amount { get; set; }
    [ProtoMember(2)] public string Currency { get; set; }
}

[ProtoContract]
public class Sample2LineItem
{
    [ProtoMember(1)] public Guid ItemId { get; set; }
    [ProtoMember(2)] public string Sku { get; set; }
    [ProtoMember(3)] public int Quantity { get; set; }
    [ProtoMember(4)] public Sample2Money UnitPrice { get; set; }
}

[ProtoContract]
public class Sample2Tag
{
    [ProtoMember(1)] public string Value { get; set; }
}
