using Orleans;

namespace ECK1.CommandsAPI.Dto.Sample;

[GenerateSerializer]
public sealed class Attachment
{
    [Id(0)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Id(1)]
    public string FileName { get; set; } = string.Empty;

    [Id(2)]
    public string Url { get; set; } = string.Empty;
}