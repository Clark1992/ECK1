namespace ECK1.CommandsAPI.Domain.Sample2s;

public class Sample2Customer
{
    private Sample2Customer()
    {
    }

    public Sample2Customer(string email, string segment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(segment);

        this.Email = email;
        this.Segment = segment;
        this.CustomerId = Guid.NewGuid();
    }

    public Guid CustomerId { get; private set; }
    public string Email { get; private set; }
    public string Segment { get; private set; }

    public Sample2Customer ChangeEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        this.Email = email;

        return this;
    }

    public Sample2Customer DeepClone() => new()
    {
        CustomerId = CustomerId,
        Email = Email,
        Segment = Segment,
    };
}
