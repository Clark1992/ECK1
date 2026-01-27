using Confluent.Kafka.Extensions.OpenTelemetry;
using OpenTelemetry.Trace;

namespace ECK1.Kafka.OpenTelemetry;

public static class KafkaOpenTelemetryExtensions
{
    public static TracerProviderBuilder AddKafkaInstrumentation(this TracerProviderBuilder tracing) =>
        tracing.AddConfluentKafkaInstrumentation();
}
