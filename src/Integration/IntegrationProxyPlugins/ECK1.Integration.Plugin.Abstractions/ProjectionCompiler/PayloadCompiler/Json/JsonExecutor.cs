using System.Text.Json;

namespace ECK1.Integration.Plugin.Abstractions.ProjectionCompiler.PayloadCompiler.Json;

public static class JsonExecutor
{
    public static void Execute<TEvent, TRecord>(
        Utf8JsonWriter writer,
        ReadOnlySpan<JsonOp<TEvent, TRecord>> ops,
        in JsonExecutionContext<TEvent, TRecord> ctx)
    {
        foreach (ref readonly var op in ops)
        {
            switch (op.Kind)
            {
                case OpKind.StartObject:
                    writer.WriteStartObject();
                    break;

                case OpKind.EndObject:
                    writer.WriteEndObject();
                    break;

                case OpKind.StartArray:
                    writer.WriteStartArray();
                    break;

                case OpKind.EndArray:
                    writer.WriteEndArray();
                    break;

                case OpKind.Name:
                    writer.WritePropertyName(op.Name);
                    break;

                case OpKind.Value:
                    op.Execute(writer, ctx);
                    break;
            }
        }
    }
}

