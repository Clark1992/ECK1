using MediatR;

namespace ECK1.QueriesAPI.Queries.History;

public record GetEntityHistoryQuery(string EntityType, Guid EntityId) : IRequest<EntityHistoryResponse>;

public record EntityHistoryResponse(IReadOnlyList<EntityHistoryEvent> Events);

public record EntityHistoryEvent(
    Guid EventId,
    string EventType,
    DateTime OccurredAt,
    int EntityVersion,
    string Payload);
