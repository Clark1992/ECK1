using Confluent.SchemaRegistry;

namespace ECK1.Kafka.ProtoBuf;

public class ProtoSerdeConfig
{
    public bool UseLatestVersion { get; set; } = true;
    public SubjectNameStrategy SubjectNameStrategy { get; set; } = SubjectNameStrategy.Topic;
}
