namespace ECK1.Gateway.Realtime;

public class RealtimeConfig
{
    public const string Section = "Realtime";

    public string FeedbackTopic { get; set; } = string.Empty;
    public string RedisConnectionString { get; set; } = string.Empty;
    public int SendTimeoutMs { get; set; } = 5000;
    public int MaxBufferSize { get; set; } = 100;
}
