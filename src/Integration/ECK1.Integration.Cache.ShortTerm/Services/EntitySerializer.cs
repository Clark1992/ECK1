using System.Buffers;
using ProtoBuf;

namespace ECK1.Integration.Cache.ShortTerm.Services;

public static class EntitySerializer
{
    public static byte[] ToBytes<T>(T value)
    {
        var buffer = new ArrayBufferWriter<byte>();
        Serializer.Serialize(buffer, value);
        return buffer.WrittenMemory.ToArray();
    }

    public static T FromBytes<T>(byte[] bytes)
    {
        var seq = new ReadOnlySequence<byte>(bytes);
        return Serializer.Deserialize<T>(seq);
    }
}