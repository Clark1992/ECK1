using MediatR;

namespace ECK1.QueriesAPI.Queries.Analytics;

public enum AnalyticsDataset
{
    Events,
    Samples,
    Orders,
}

public enum AnalyticsBucket
{
    Auto,
    Hour,
    Day,
    Week,
}

public enum AnalyticsAggregation
{
    Sum,
    Avg,
    Min,
    Max,
}

public enum AnalyticsTrendMetric
{
    EventCount,
    UniqueEntities,
    SampleCount,
    AttachmentsCount,
    OrderCount,
    OrderGrossValue,
    Units,
}

public enum AnalyticsBreakdownMetric
{
    EventCount,
    UniqueEntities,
    SampleCount,
    AttachmentsCount,
    AttachmentCoverage,
    OrderCount,
    OrderGrossValue,
    AvgOrderValue,
    Units,
}

public enum AnalyticsDimension
{
    EntityType,
    EventType,
    SampleCountry,
    Status,
    CustomerSegment,
    ShippingCountry,
}

public sealed class GetAnalyticsOverviewQuery : IRequest<AnalyticsOverviewResponse>
{
    public DateTime From { get; set; } = DateTime.UtcNow.AddDays(-30);

    public DateTime To { get; set; } = DateTime.UtcNow;
}

public sealed class GetAnalyticsTrendQuery : IRequest<AnalyticsTrendResponse>
{
    public DateTime From { get; set; } = DateTime.UtcNow.AddDays(-30);

    public DateTime To { get; set; } = DateTime.UtcNow;

    public AnalyticsDataset Dataset { get; set; } = AnalyticsDataset.Events;

    public AnalyticsTrendMetric Metric { get; set; } = AnalyticsTrendMetric.EventCount;

    public AnalyticsBucket Bucket { get; set; } = AnalyticsBucket.Auto;

    public AnalyticsAggregation Aggregation { get; set; } = AnalyticsAggregation.Sum;

    public AnalyticsDimension? GroupBy { get; set; }
}

public sealed class GetAnalyticsBreakdownQuery : IRequest<AnalyticsBreakdownResponse>
{
    public DateTime From { get; set; } = DateTime.UtcNow.AddDays(-30);

    public DateTime To { get; set; } = DateTime.UtcNow;

    public AnalyticsDataset Dataset { get; set; } = AnalyticsDataset.Events;

    public AnalyticsBreakdownMetric Metric { get; set; } = AnalyticsBreakdownMetric.EventCount;

    public AnalyticsDimension Dimension { get; set; } = AnalyticsDimension.EntityType;

    public AnalyticsAggregation Aggregation { get; set; } = AnalyticsAggregation.Sum;

    public int Top { get; set; } = 8;
}

public sealed record AnalyticsOverviewResponse(IReadOnlyCollection<AnalyticsKpiItem> Kpis);

public sealed record AnalyticsKpiItem(
    string Key,
    string Label,
    double Value,
    string Unit,
    string Hint);

public sealed record AnalyticsTrendResponse(
    string Title,
    string ValueLabel,
    AnalyticsBucket AppliedBucket,
    IReadOnlyCollection<AnalyticsTrendSeries> Series);

public sealed record AnalyticsTrendSeries(
    string Key,
    string Label,
    IReadOnlyCollection<AnalyticsTrendPoint> Points);

public sealed record AnalyticsTrendPoint(DateTime Time, double Value);

public sealed record AnalyticsBreakdownResponse(
    string Title,
    string ValueLabel,
    IReadOnlyCollection<AnalyticsBreakdownItem> Items);

public sealed record AnalyticsBreakdownItem(
    string Key,
    string Label,
    double Value);

internal sealed record AnalyticsTrendDefinition(
    string Title,
    string ValueLabel,
    AnalyticsBucket AppliedBucket,
    AnalyticsDimension? GroupBy,
    string Sql);

internal sealed record AnalyticsBreakdownDefinition(
    string Title,
    string ValueLabel,
    AnalyticsDimension Dimension,
    string Sql);