using ECK1.CommandsAPI.Domain;

namespace ECK1.CommandsAPI.Data;

public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(IAggregateRoot aggregate, string message)
        : base($"{aggregate.GetType().Name} [{aggregate.Id}] version conflict. Message {message}.")
    {
        Type = aggregate.GetType().Name;
        AggregateId = aggregate.Id;
    }

    public string Type { get; }
    public Guid AggregateId { get; }
}
