using Orleans;

namespace ECK1.CommandsAPI.Dto.Sample2;

[GenerateSerializer]
public sealed class Customer
{
    [Id(0)]
    public Guid? CustomerId { get; set; }

    [Id(1)]
    public string Email { get; set; } = string.Empty;

    [Id(2)]
    public string Segment { get; set; } = string.Empty;
}
