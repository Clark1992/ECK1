using Orleans;

namespace ECK1.CommandsAPI.Dto.Common;

[GenerateSerializer]
public sealed class Address
{
    [Id(0)]
    public string Street { get; set; } = string.Empty;

    [Id(1)]
    public string City { get; set; } = string.Empty;

    [Id(2)]
    public string Country { get; set; } = string.Empty;
}
