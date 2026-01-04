using Bogus;

namespace ECK1.TestPlatform.Services;

public sealed class FakeSample2DataFactory
{
    private readonly Faker _faker = new("en");

    public string NewEmail() => _faker.Internet.Email();

    public string NewReason() => _faker.Lorem.Sentence();

    public Sample2AddressDto NewSample2Address()
        => new(
            Id: Guid.NewGuid(),
            Street: _faker.Address.StreetAddress(),
            City: _faker.Address.City(),
            Country: _faker.Address.Country());

    public CreateSample2Request CreateSample2()
    {
        var customer = new Sample2CustomerDto(
            CustomerId: Guid.NewGuid(),
            Email: _faker.Internet.Email(),
            Segment: _faker.Commerce.Department());

        var address = NewSample2Address();

        var lineItems = new List<Sample2LineItemDto>();
        var itemsCount = _faker.Random.Int(1, 3);
        for (var i = 0; i < itemsCount; i++)
        {
            var unitPrice = new Sample2MoneyDto(
                Amount: _faker.Random.Decimal(1, 500),
                Currency: "USD");

            lineItems.Add(new Sample2LineItemDto(
                ItemId: Guid.NewGuid(),
                Sku: _faker.Random.AlphaNumeric(10).ToUpperInvariant(),
                Quantity: _faker.Random.Int(1, 10),
                UnitPrice: unitPrice));
        }

        var tags = _faker.Random.Bool(0.5f)
            ? new List<string>()
            : Enumerable.Range(0, _faker.Random.Int(1, 3))
                .Select(_ => _faker.Commerce.ProductAdjective())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        return new CreateSample2Request(
            Customer: customer,
            ShippingAddress: address,
            LineItems: lineItems,
            Tags: tags);
    }
}
