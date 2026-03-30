using MongoDB.Bson;

namespace ECK1.Integration.Plugin.Mongo;

internal static class BsonValueConverter
{
    public static BsonValue ToBsonValue(object value) => value switch
    {
        null => BsonNull.Value,
        string s => new BsonString(s),
        Guid g => new BsonBinaryData(g, GuidRepresentation.Standard),
        int i => new BsonInt32(i),
        long l => new BsonInt64(l),
        double d => new BsonDouble(d),
        float f => new BsonDouble(f),
        decimal m => BsonDecimal128.Create(m),
        DateTime dt => new BsonDateTime(dt),
        bool b => b ? BsonBoolean.True : BsonBoolean.False,
        _ => throw new InvalidOperationException($"Unsupported type '{value.GetType().Name}' for BsonValue conversion")
    };
}
