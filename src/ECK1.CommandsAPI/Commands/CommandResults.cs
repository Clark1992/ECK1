using ECK1.AsyncApi.Attributes;
using ECK1.CommandsAPI.Domain.Sample2s;
using ECK1.CommandsAPI.Domain.Samples;
using MediatR;
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
public class ConcurrencyConflict : Error
{
    public ConcurrencyConflict() { }

    public ConcurrencyConflict(string aggregate, Guid aggregateId, string message, bool retryable)
    {
        Aggregate = aggregate;
        AggregateId = aggregateId;
        ErrorMessage = message;
        Retryable = retryable;
    }

    [Id(1)] public string Aggregate { get; set; } = string.Empty;
    [Id(2)] public Guid AggregateId { get; set; }
    [Id(3)] public bool Retryable { get; set; } = true;
}
