using Orleans;

namespace ECK1.CommandsAPI.Dto.Common;

[GenerateSerializer]
public sealed class Money
{
    [Id(0)]
    public decimal Amount { get; set; }

    [Id(1)]
    public string Currency { get; set; } = string.Empty;
}