namespace ECK1.Integration.Plugin.Abstractions.ProjectionCompiler;

public sealed class ExecutionPlan<TEvent, TRecord>
{
    public string[] ColumnNames { get; init; }

    public Func<TEvent, TRecord, object[]> ColumnValues { get; init; } 
}
