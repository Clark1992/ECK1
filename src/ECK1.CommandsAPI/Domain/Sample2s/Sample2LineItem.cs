namespace ECK1.CommandsAPI.Domain.Sample2s;

public class Sample2LineItem
{
    public Guid ItemId { get; private set; }
    public string Sku { get; private set; }
    public int Quantity { get; private set; }
    public Sample2Money UnitPrice { get; private set; }

    private Sample2LineItem()
    {
    }

    public Sample2LineItem(string sku,
                           int quantity,
                           Sample2Money unitPrice)
    {
        ArgumentException.ThrowIfNullOrEmpty(sku);
        if (quantity <= 0) throw new ArgumentException("quantity must be positive");

        Sku = sku;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public Sample2LineItem DeepClone() => new()
    {
        ItemId = ItemId,
        Sku = Sku,
        Quantity = Quantity,
        UnitPrice = UnitPrice?.DeepClone(),
    };
}
