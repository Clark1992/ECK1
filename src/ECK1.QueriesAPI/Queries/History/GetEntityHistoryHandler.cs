using ClickHouse.Client.ADO;
using ClickHouse.Client.ADO.Parameters;
using MediatR;

namespace ECK1.QueriesAPI.Queries.History;

public sealed class GetEntityHistoryHandler : IRequestHandler<GetEntityHistoryQuery, EntityHistoryResponse>
{
    private readonly ClickHouseConnection _connection;

    public GetEntityHistoryHandler(ClickHouseConnection connection)
    {
        _connection = connection;
    }

    public async Task<EntityHistoryResponse> Handle(GetEntityHistoryQuery request, CancellationToken ct)
    {
        const string sql = """
            SELECT event_id, event_type, occurred_at, entity_version, payload
            FROM integration_events_raw FINAL
            WHERE entity_type = {entityType:String}
              AND entity_id = {entityId:UUID}
            ORDER BY entity_version ASC
            """;

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.Add(new ClickHouseDbParameter { ParameterName = "entityType", Value = request.EntityType });
        cmd.Parameters.Add(new ClickHouseDbParameter { ParameterName = "entityId", Value = request.EntityId });

        var events = new List<EntityHistoryEvent>();

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            events.Add(new EntityHistoryEvent(
                EventId: reader.GetGuid(0),
                EventType: reader.GetString(1),
                OccurredAt: reader.GetDateTime(2),
                EntityVersion: reader.GetInt32(3),
                Payload: reader.GetString(4)));
        }

        return new EntityHistoryResponse(events);
    }
}
