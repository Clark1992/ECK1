namespace ECK1.CommandsAPI.Domain.Sample2s;

public class Sample2Money
{
    public decimal Amount { get; private set; }
    public string Currency { get; private set; }

    private Sample2Money()
    {
    }

    public Sample2Money(decimal amount, string currency)
    {
        if (amount <= 0) throw new ArgumentException("amount must be positive");

        Amount = amount;
        Currency = currency;
    }

    public Sample2Money DeepClone() => new()
    {
        Amount = Amount,
        Currency = Currency,
    };
}
