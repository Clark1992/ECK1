namespace ECK1.AsyncApi.Attributes;

[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class)]
public class TopicAttribute(string Topic = null, string TopicConfigKey = null) : Attribute
{
    public string Topic { get; set; } = Topic;
    public string TopicConfigKey { get; set; } = TopicConfigKey;
}
