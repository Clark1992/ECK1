namespace ECK1.Kafka.Integrations;

internal static class SN
{
    public static string GetBrokerPassword(string token) => $"token:{token}";

    public static string GetSrPassword(string token) => $"public:{token}";
}
