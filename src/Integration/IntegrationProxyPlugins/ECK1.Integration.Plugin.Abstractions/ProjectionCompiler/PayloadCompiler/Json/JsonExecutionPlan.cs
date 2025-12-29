using System.Text.Json;

namespace ECK1.Integration.Plugin.Abstractions.ProjectionCompiler.PayloadCompiler.Json;

public sealed class JsonExecutionPlan<TEvent, TRecord>(JsonOp<TEvent, TRecord>[] ops)
{
    public void Execute(
        Utf8JsonWriter writer,
        TEvent evt,
        TRecord record)
    {
        var ctx = new JsonExecutionContext<TEvent, TRecord>(evt, record);
        JsonExecutor.Execute(writer, ops, ctx);
    }
}

