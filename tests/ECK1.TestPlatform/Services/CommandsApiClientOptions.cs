namespace ECK1.TestPlatform.Services;

public sealed class CommandsApiClientOptions
{
    public const string SectionName = "CommandsApi";

    public string BaseUrl { get; set; } = "";

    public int TimeoutSeconds { get; set; } = 30;
}
