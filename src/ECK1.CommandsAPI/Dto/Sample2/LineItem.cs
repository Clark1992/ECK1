using ECK1.CommandsAPI.Dto.Common;
using Orleans;

namespace ECK1.CommandsAPI.Dto.Sample2;

[GenerateSerializer]
public sealed class LineItem
{
    [Id(0)]
    public string Sku { get; set; } = string.Empty;

    [Id(1)]
    public int Quantity { get; set; }

    [Id(2)]
    public Money UnitPrice { get; set; }
}