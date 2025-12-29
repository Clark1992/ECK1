using System.Text.Json;

namespace ECK1.Integration.Plugin.Abstractions.ProjectionCompiler.PayloadCompiler.Json;

public enum OpKind : byte
{
    StartObject,
    EndObject,
    StartArray,
    EndArray,
    Name,
    Value
}

public readonly struct JsonOp<TEvent, TRecord>
{
    public readonly OpKind Kind;
    public readonly string Name;
    public readonly Action<Utf8JsonWriter, JsonExecutionContext<TEvent, TRecord>> Execute;

    private JsonOp(
        OpKind kind,
        string name = null,
        Action<Utf8JsonWriter, JsonExecutionContext<TEvent, TRecord>> execute = null)
    {
        Kind = kind;
        Name = name;
        Execute = execute;
    }

    public static JsonOp<TEvent, TRecord> ObjStart() =>
        new(OpKind.StartObject);

    public static JsonOp<TEvent, TRecord> ObjEnd() =>
        new(OpKind.EndObject);

    public static JsonOp<TEvent, TRecord> ArrStart() =>
        new(OpKind.StartArray);

    public static JsonOp<TEvent, TRecord> ArrEnd() =>
        new(OpKind.EndArray);

    public static JsonOp<TEvent, TRecord> NameOf(string name) =>
        new(OpKind.Name, name: name);

    public static JsonOp<TEvent, TRecord> Emit(
        Action<Utf8JsonWriter, JsonExecutionContext<TEvent, TRecord>> exec) =>
        new(OpKind.Value, execute: exec);
}

