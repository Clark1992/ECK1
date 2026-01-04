using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace ECK1.QueriesAPI.Views;

public class Sample2View
{
    [JsonIgnore]
    public ObjectId Id { get; set; }

    public Guid Sample2Id { get; set; }
    public Sample2CustomerView Customer { get; set; }
    public Sample2AddressView ShippingAddress { get; set; }
    public List<Sample2LineItemView> LineItems { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public int Status { get; set; }
}

[BsonNoId]
public class Sample2CustomerView
{
    public Guid CustomerId { get; set; }
    public string Email { get; set; }
    public string Segment { get; set; }
}

[BsonNoId]
public class Sample2AddressView
{
    [JsonIgnore]
    public Guid Id { get; set; }
    public string Street { get; set; }
    public string City { get; set; }
    public string Country { get; set; }
}

[BsonNoId]
public class Sample2MoneyView
{
    public decimal Amount { get; set; }
    public string Currency { get; set; }
}

[BsonNoId]
public class Sample2LineItemView
{
    public Guid ItemId { get; set; }
    public string Sku { get; set; }
    public int Quantity { get; set; }
    public Sample2MoneyView UnitPrice { get; set; }
}
