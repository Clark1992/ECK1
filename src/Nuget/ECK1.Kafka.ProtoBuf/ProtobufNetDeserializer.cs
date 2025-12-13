using Confluent.Kafka;
using ProtoBuf;

namespace ECK1.Kafka.ProtoBuf;

public class ProtobufNetDeserializer<T>() : IAsyncDeserializer<T>
    where T : class
{
    public async Task<T> DeserializeAsync(ReadOnlyMemory<byte> data, bool isNull, Confluent.Kafka.SerializationContext context)
    {
        if (isNull || data.IsEmpty)
        {
            return null;
        }

        // Confluent framing: magic byte (0) + schemaId (4 bytes)
        const byte MagicByte = 0;
        if (data.Span[0] != MagicByte)
        {
            throw new InvalidOperationException($"Unexpected magic byte: {data.Span[0]}");
        }

        //int schemaId = BitConverter.ToInt32([data.Span[4], data.Span[3], data.Span[2], data.Span[1]], 0);

        // The rest of the bytes starting from offset 5
        var payload = data.Span.Slice(5).ToArray();

        using var ms = new MemoryStream(payload);
        var result = Serializer.Deserialize<T>(ms);
        return result;
    }
}