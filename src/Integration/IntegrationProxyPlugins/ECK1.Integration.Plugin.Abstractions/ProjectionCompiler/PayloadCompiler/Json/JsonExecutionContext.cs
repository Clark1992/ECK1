namespace ECK1.Integration.Plugin.Abstractions.ProjectionCompiler.PayloadCompiler.Json;

public readonly struct JsonExecutionContext<TEvent, TRecord>(
    TEvent evt,
    TRecord record,
    object item = null)
{
    public readonly TEvent Event = evt;
    public readonly TRecord Record = record;
    public readonly object Item = item;

    public JsonExecutionContext<TEvent, TRecord> WithItem(object item) =>
      new(Event, Record, item);
}

