using System.Text.Json;

namespace ECK1.Integration.Plugin.Abstractions.ProjectionCompiler.PayloadCompiler.Json;

internal sealed class JsonItemExecutionPlan<TEvent, TRecord, TItem>(JsonOp<TEvent, TRecord>[] ops)
{
    public void Execute(
        Utf8JsonWriter writer,
        JsonExecutionContext<TEvent, TRecord> parentCtx,
        TItem item)
    {
        var ctx = parentCtx.WithItem(item);
        JsonExecutor.Execute(writer, ops, ctx);
    }
}


