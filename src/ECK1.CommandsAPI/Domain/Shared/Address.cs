namespace ECK1.CommandsAPI.Domain.Shared;

public class Address
{
    public Guid Id { get; private set; }
    public string Street { get; private set; }
    public string City { get; private set; }
    public string Country { get; private set; }

    private Address() { }

    public Address(string street, string city, string country)
    {
        if (string.IsNullOrWhiteSpace(street)) throw new ArgumentException("Street is required");
        if (string.IsNullOrWhiteSpace(city)) throw new ArgumentException("City is required");
        if (string.IsNullOrWhiteSpace(country)) throw new ArgumentException("Country is required");

        Street = street;
        City = city;
        Country = country;
    }

    public Address DeepClone() => new()
    {
        Street = Street,
        City = City,
        Country = Country,
        Id = Id
    };
}