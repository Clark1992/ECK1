namespace ECK1.E2E.Tests.API;

public class E2ESettings
{
    public const string Section = "E2E";

    public string GatewayUrl { get; set; } = string.Empty;
    public AuthSettings Auth { get; set; } = new();
}

public class AuthSettings
{
    public string Url { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ServiceAccountPat { get; set; } = string.Empty;
    public string UserLogin { get; set; } = string.Empty;
    public string UserPassword { get; set; } = string.Empty;
    public string AdminLogin { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;
}
