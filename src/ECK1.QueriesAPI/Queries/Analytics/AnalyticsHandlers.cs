using System.Globalization;
using ClickHouse.Client.ADO;
using ClickHouse.Client.ADO.Parameters;
using MediatR;

namespace ECK1.QueriesAPI.Queries.Analytics;

public sealed class GetAnalyticsOverviewHandler : IRequestHandler<GetAnalyticsOverviewQuery, AnalyticsOverviewResponse>
{
    private readonly ClickHouseConnection _connection;

    public GetAnalyticsOverviewHandler(ClickHouseConnection connection)
    {
        _connection = connection;
    }

    public async Task<AnalyticsOverviewResponse> Handle(GetAnalyticsOverviewQuery request, CancellationToken ct)
    {
        AnalyticsQueryBuilder.ValidateRange(request.From, request.To);

        const string eventsSql = """
            SELECT
                count() AS total_events,
                uniqExact(tuple(entity_type, entity_id)) AS unique_entities
            FROM integration_events_raw FINAL
            WHERE occurred_at >= {from:DateTime}
              AND occurred_at <= {to:DateTime}
            """;

        var (totalEvents, uniqueEntities) = await ExecuteEventTotalsAsync(eventsSql, request, ct);

        var samplesSql = $$"""
            WITH
            {{AnalyticsQueryBuilder.BuildSampleSnapshotsCte()}}
            SELECT
                count() AS samples_updated,
                coalesce(avg(if(attachments_count > 0, 100.0, 0.0)), 0.0) AS attachment_coverage
            FROM sample_snapshots
            WHERE updated_at >= {from:DateTime}
            """;

        var (samplesUpdated, attachmentCoverage) = await ExecutePairAsync(samplesSql, request, ct);

        var ordersSql = $$"""
            WITH
            {{AnalyticsQueryBuilder.BuildOrderSnapshotsCte()}}
            SELECT
                count() AS orders_updated,
                coalesce(sum(order_total_amount), 0.0) AS gross_value,
                coalesce(avg(order_total_amount), 0.0) AS avg_order_value
            FROM order_snapshots
            WHERE updated_at >= {from:DateTime}
            """;

        var (ordersUpdated, grossValue, avgOrderValue) = await ExecuteTripleAsync(ordersSql, request, ct);

        var kpis = new List<AnalyticsKpiItem>
        {
            new("events", "Events in period", totalEvents, "count", "Raw event flow across all entity types."),
            new("entities", "Entities touched", uniqueEntities, "count", "Distinct entities that emitted at least one event in the period."),
            new("samples", "Samples updated", samplesUpdated, "count", "Latest sample state changes observed in the period."),
            new("attachmentsCoverage", "Attachment coverage", attachmentCoverage, "percent", "Share of updated samples that currently have at least one attachment."),
            new("orders", "Orders updated", ordersUpdated, "count", "Latest order state changes observed in the period."),
            new("grossValue", "Gross order value", grossValue, "amount", "Sum of line-item totals from the latest updated orders in the period."),
            new("avgOrderValue", "Average order value", avgOrderValue, "amount", "Average total value of the latest updated orders in the period."),
        };

        return new AnalyticsOverviewResponse(kpis);
    }

    private async Task<(double TotalEvents, double UniqueEntities)> ExecuteEventTotalsAsync(
        string sql,
        GetAnalyticsOverviewQuery request,
        CancellationToken ct)
    {
        await using var cmd = CreateCommand(sql, request.From, request.To, top: null);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct))
        {
            return (0, 0);
        }

        return (ReadDouble(reader, 0), ReadDouble(reader, 1));
    }

    private async Task<(double First, double Second)> ExecutePairAsync(
        string sql,
        GetAnalyticsOverviewQuery request,
        CancellationToken ct)
    {
        await using var cmd = CreateCommand(sql, request.From, request.To, top: null);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct))
        {
            return (0, 0);
        }

        return (ReadDouble(reader, 0), ReadDouble(reader, 1));
    }

    private async Task<(double First, double Second, double Third)> ExecuteTripleAsync(
        string sql,
        GetAnalyticsOverviewQuery request,
        CancellationToken ct)
    {
        await using var cmd = CreateCommand(sql, request.From, request.To, top: null);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct))
        {
            return (0, 0, 0);
        }

        return (ReadDouble(reader, 0), ReadDouble(reader, 1), ReadDouble(reader, 2));
    }

    private ClickHouseCommand CreateCommand(string sql, DateTime from, DateTime to, int? top)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.Add(new ClickHouseDbParameter { ParameterName = "from", Value = from });
        cmd.Parameters.Add(new ClickHouseDbParameter { ParameterName = "to", Value = to });

        if (top.HasValue)
        {
            cmd.Parameters.Add(new ClickHouseDbParameter { ParameterName = "top", Value = top.Value });
        }

        return cmd;
    }

    private static double ReadDouble(System.Data.Common.DbDataReader reader, int index)
    {
        if (reader.IsDBNull(index))
        {
            return 0;
        }

        return Convert.ToDouble(reader.GetValue(index), CultureInfo.InvariantCulture);
    }
}

public sealed class GetAnalyticsTrendHandler : IRequestHandler<GetAnalyticsTrendQuery, AnalyticsTrendResponse>
{
    private readonly ClickHouseConnection _connection;

    public GetAnalyticsTrendHandler(ClickHouseConnection connection)
    {
        _connection = connection;
    }

    public async Task<AnalyticsTrendResponse> Handle(GetAnalyticsTrendQuery request, CancellationToken ct)
    {
        var definition = AnalyticsQueryBuilder.BuildTrend(request);

        await using var cmd = CreateCommand(definition.Sql, request.From, request.To, top: null);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var series = new Dictionary<string, List<AnalyticsTrendPoint>>(StringComparer.Ordinal);
        while (await reader.ReadAsync(ct))
        {
            var timestamp = reader.GetDateTime(0);
            var key = reader.IsDBNull(1) ? "total" : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? "total";
            var value = reader.IsDBNull(2)
                ? 0
                : Convert.ToDouble(reader.GetValue(2), CultureInfo.InvariantCulture);

            if (!series.TryGetValue(key, out var points))
            {
                points = [];
                series[key] = points;
            }

            points.Add(new AnalyticsTrendPoint(timestamp, value));
        }

        var mappedSeries = series
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => new AnalyticsTrendSeries(
                entry.Key,
                AnalyticsQueryBuilder.FormatDimensionLabel(definition.GroupBy, entry.Key),
                entry.Value.OrderBy(point => point.Time).ToArray()))
            .ToArray();

        return new AnalyticsTrendResponse(definition.Title, definition.ValueLabel, definition.AppliedBucket, mappedSeries);
    }

    private ClickHouseCommand CreateCommand(string sql, DateTime from, DateTime to, int? top)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.Add(new ClickHouseDbParameter { ParameterName = "from", Value = from });
        cmd.Parameters.Add(new ClickHouseDbParameter { ParameterName = "to", Value = to });

        if (top.HasValue)
        {
            cmd.Parameters.Add(new ClickHouseDbParameter { ParameterName = "top", Value = top.Value });
        }

        return cmd;
    }
}

public sealed class GetAnalyticsBreakdownHandler : IRequestHandler<GetAnalyticsBreakdownQuery, AnalyticsBreakdownResponse>
{
    private readonly ClickHouseConnection _connection;

    public GetAnalyticsBreakdownHandler(ClickHouseConnection connection)
    {
        _connection = connection;
    }

    public async Task<AnalyticsBreakdownResponse> Handle(GetAnalyticsBreakdownQuery request, CancellationToken ct)
    {
        var definition = AnalyticsQueryBuilder.BuildBreakdown(request);
        var top = Math.Clamp(request.Top, 1, 25);

        await using var cmd = CreateCommand(definition.Sql, request.From, request.To, top);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var items = new List<AnalyticsBreakdownItem>();
        while (await reader.ReadAsync(ct))
        {
            var key = reader.IsDBNull(0) ? "Unknown" : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? "Unknown";
            var value = reader.IsDBNull(1)
                ? 0
                : Convert.ToDouble(reader.GetValue(1), CultureInfo.InvariantCulture);

            items.Add(new AnalyticsBreakdownItem(
                key,
                AnalyticsQueryBuilder.FormatDimensionLabel(definition.Dimension, key),
                value));
        }

        return new AnalyticsBreakdownResponse(definition.Title, definition.ValueLabel, items);
    }

    private ClickHouseCommand CreateCommand(string sql, DateTime from, DateTime to, int? top)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.Add(new ClickHouseDbParameter { ParameterName = "from", Value = from });
        cmd.Parameters.Add(new ClickHouseDbParameter { ParameterName = "to", Value = to });

        if (top.HasValue)
        {
            cmd.Parameters.Add(new ClickHouseDbParameter { ParameterName = "top", Value = top.Value });
        }

        return cmd;
    }
}