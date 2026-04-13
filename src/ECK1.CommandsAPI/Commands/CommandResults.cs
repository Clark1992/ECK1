using Orleans;

namespace ECK1.CommandsAPI.Commands;

public interface ICommandResult { }

[GenerateSerializer]
public class Success : ICommandResult 
{
    public Success() { }
    public Success(Guid id, List<Guid> eventIds)
    {
        Id = id;
        EventIds = eventIds;
    }

    [Id(0)]
    public Guid Id { get; set; }
    [Id(1)]
    public List<Guid> EventIds { get; set; }
}

[GenerateSerializer]
public class NotFound : ICommandResult { }

[GenerateSerializer]
public class Error : ICommandResult { [Id(0)] public string ErrorMessage { get; set; } }

[GenerateSerializer]
public class VersionConflict : Error
{
    public VersionConflict() { }

    public VersionConflict(
        int currentVersion,
        int expectedVersion,
        string aggregate,
        Guid aggregateId)
    {
        CurrentVersion = currentVersion;
        ExpectedVersion = expectedVersion;
        Aggregate = aggregate;
        AggregateId = aggregateId;
        ErrorMessage = $"{Aggregate} entity: {AggregateId} - Version conflict: expected {expectedVersion}, but current is {currentVersion}.";
    }

    public VersionConflict(
        int currentVersion,
        string aggregate,
        Guid aggregateId,
        string message)
    {
        CurrentVersion = currentVersion;
        Aggregate = aggregate;
        AggregateId = aggregateId;
        ErrorMessage = $"Error saving {Aggregate} entity: {AggregateId}, version: {currentVersion}.\n{message}";
    }

    [Id(1)] public int CurrentVersion { get; set; }
    [Id(2)] public int ExpectedVersion { get; set; }
    [Id(3)] public string Aggregate { get; set; } = string.Empty;
    [Id(4)] public Guid AggregateId { get; set; }
}

public static class CommandResultHelper
{
    public static (bool, string, string) GetOutcomeData(this ICommandResult result) => result switch
    {
        Success => (true, "OK", string.Empty),
        VersionConflict vc => (false, "VERSION_CONFLICT", vc.ErrorMessage),
        //ConcurrencyConflict cc => (false, "CONCURRENCY_CONFLICT", cc.ErrorMessage),
        NotFound => (false, "NOT_FOUND", "Entity not found."),
        Error err => (false, "ERROR", err.ErrorMessage),
        _ => (false, "UNKNOWN", string.Empty),
    };
}