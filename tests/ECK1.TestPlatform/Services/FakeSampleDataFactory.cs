using Bogus;

namespace ECK1.TestPlatform.Services;

public sealed class FakeSampleDataFactory
{
    private readonly Faker _faker = new("en");

    public CreateSampleRequest CreateSample(bool withAddress)
    {
        var name = _faker.Commerce.ProductName();
        var description = _faker.Lorem.Paragraph();

        SampleAddressDto? address = null;
        if (withAddress)
        {
            address = new SampleAddressDto(
                Street: _faker.Address.StreetAddress(),
                City: _faker.Address.City(),
                Country: _faker.Address.Country());
        }

        return new CreateSampleRequest(name, description, address);
    }

    public string NewName() => _faker.Commerce.ProductName();

    public string NewDescription() => _faker.Lorem.Sentence();
}
