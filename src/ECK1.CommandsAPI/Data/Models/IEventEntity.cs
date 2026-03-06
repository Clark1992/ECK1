using ECK1.CommandsAPI.Domain;

namespace ECK1.CommandsAPI.Data.Models;

public interface IEventEntity
{
    Guid AggregateId { get; }
    Guid EventId { get; }
    int Version { get; }
    IDomainEvent ToDomainEvent();

    static abstract IEventEntity FromDomainEvent(IDomainEvent domainEvent, int version);
}