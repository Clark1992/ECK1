namespace ECK1.TestPlatform.Services;

public sealed class ProxyServiceConfig
{
    public const string SectionName = "ProxyServices";

    public Dictionary<string, string> Plugins { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
