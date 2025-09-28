namespace ECK1.CommandsAPI.Domain.Samples;

public class SampleAddress
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Street { get; private set; }
    public string City { get; private set; }
    public string Country { get; private set; }

    private SampleAddress() { }

    public SampleAddress(string street, string city, string country)
    {
        if (string.IsNullOrWhiteSpace(street)) throw new ArgumentException("Street is required");
        if (string.IsNullOrWhiteSpace(city)) throw new ArgumentException("City is required");
        if (string.IsNullOrWhiteSpace(country)) throw new ArgumentException("Country is required");

        Street = street;
        City = city;
        Country = country;
    }
}