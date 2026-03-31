namespace ECK1.CommonUtils.Chaos;

public class ChaosConfig
{
    public static string Section => "Chaos";
    public bool Enabled { get; set; }
    public List<string> Scenarios { get; set; } = [];
}
