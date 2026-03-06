using ECK1.CommandsAPI.Domain;

namespace ECK1.CommandsAPI.Data;

public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(IAggregateRoot aggregate, int actualVersion)
        : base($"{aggregate.GetType().Name} [{aggregate.Id}] version conflict. Expected version {aggregate.Version}, current {actualVersion}.")
    {
        Type = aggregate.GetType().Name;
        AggregateId = aggregate.Id;
    }

    public ConcurrencyConflictException(IAggregateRoot aggregate, int actualVersion, string phase)
    : base($"{aggregate.GetType().Name} [{aggregate.Id}] version conflict. Expected version {aggregate.Version}, current {actualVersion} during [{phase}].")
    {
        Type = aggregate.GetType().Name;
        AggregateId = aggregate.Id;
    }

    public string Type { get; }
    public Guid AggregateId { get; }
}
