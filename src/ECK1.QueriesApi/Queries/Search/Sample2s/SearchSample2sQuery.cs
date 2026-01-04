using ECK1.QueriesAPI.Views;

namespace ECK1.QueriesAPI.Queries.Search.Sample2s;

public record SearchSample2sQuery : PagedQuery<Sample2View>
{
    public string Q { get; set; } = string.Empty;

    public bool? HasCustomer { get; set; }
    public bool? HasShippingAddress { get; set; }
    public bool? HasLineItems { get; set; }

    public List<string> Tags { get; set; } = new();
    public List<string> ExcludeTags { get; set; } = new();

    public List<string> Statuses { get; set; } = new();

    public decimal? LineItemUnitPriceAmountGt { get; set; }
    public bool? HasLineItemUnitPriceAmountGt { get; set; }

    public decimal? LineItemUnitPriceAmountLt { get; set; }
    public bool? HasLineItemUnitPriceAmountLt { get; set; }

    public List<string> Countries { get; set; } = new();
    public List<string> Cities { get; set; } = new();
    public List<string> Streets { get; set; } = new();
}
