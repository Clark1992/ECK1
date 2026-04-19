using ECK1.QueriesAPI.Queries.Analytics;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit.Abstractions;

namespace ECK1.QueriesAPI.Tests.Queries.Analytics;

public sealed class AnalyticsQueryBuilderTests(ITestOutputHelper output)
{
    [Fact]
    public void ResolveBucket_ForAutoBucket_ReturnsExpectedBucket()
    {
        // Arrange
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var shortRangeBucket = AnalyticsQueryBuilder.ResolveBucket(from, from.AddDays(2), AnalyticsBucket.Auto);
        var mediumRangeBucket = AnalyticsQueryBuilder.ResolveBucket(from, from.AddDays(30), AnalyticsBucket.Auto);
        var longRangeBucket = AnalyticsQueryBuilder.ResolveBucket(from, from.AddDays(120), AnalyticsBucket.Auto);

        // Assert
        using var scope = new AssertionScope();
        shortRangeBucket.Should().Be(AnalyticsBucket.Hour);
        mediumRangeBucket.Should().Be(AnalyticsBucket.Day);
        longRangeBucket.Should().Be(AnalyticsBucket.Week);
    }

    [Fact]
    public void ValidateRange_WhenFromIsNotEarlierThanTo_ThrowsArgumentException()
    {
        // Arrange
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from;

        // Act
        Action act = () => AnalyticsQueryBuilder.ValidateRange(from, to);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("'From' must be earlier than 'To'.");
    }

    [Fact]
    public void FormatDimensionLabel_ForKnownValues_ReturnsFriendlyLabels()
    {
        // Arrange

        // Act
        var entityLabel = AnalyticsQueryBuilder.FormatDimensionLabel(AnalyticsDimension.EntityType, "ECK1.Sample2");
        var statusLabel = AnalyticsQueryBuilder.FormatDimensionLabel(AnalyticsDimension.Status, "3");
        var totalLabel = AnalyticsQueryBuilder.FormatDimensionLabel(null, "");

        // Assert
        using var scope = new AssertionScope();
        entityLabel.Should().Be("Orders");
        statusLabel.Should().Be("Shipped");
        totalLabel.Should().Be("Total");
    }

    [Fact]
    public void BuildTrend_ForEventsGroupedByEntityType_ReturnsExpectedDefinitionAndSql()
    {
        // Arrange
        var request = new GetAnalyticsTrendQuery
        {
            From = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            To = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            Dataset = AnalyticsDataset.Events,
            Metric = AnalyticsTrendMetric.EventCount,
            Bucket = AnalyticsBucket.Day,
            GroupBy = AnalyticsDimension.EntityType,
        };

        var expectedSql = """
            SELECT
                toStartOfDay(occurred_at) AS time,
                entity_type AS series_key,
                count() AS value
            FROM integration_events_raw FINAL
            WHERE occurred_at >= {from:DateTime}
              AND occurred_at <= {to:DateTime}
            GROUP BY time, series_key
            ORDER BY time, series_key
            """;

        // Act
        var actual = AnalyticsQueryBuilder.BuildTrend(request);
        output.WriteLine(actual.Sql);

        // Assert
        using var scope = new AssertionScope();
        actual.Title.Should().Be("Event flow");
        actual.ValueLabel.Should().Be("Events");
        actual.AppliedBucket.Should().Be(AnalyticsBucket.Day);
        actual.GroupBy.Should().Be(AnalyticsDimension.EntityType);
        NormalizeSql(actual.Sql).Should().Be(NormalizeSql(expectedSql));
    }

    [Fact]
    public void BuildTrend_ForSampleAttachmentsByCountry_ReturnsExpectedDefinitionAndSql()
    {
        // Arrange
        var request = new GetAnalyticsTrendQuery
        {
            From = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            To = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc),
            Dataset = AnalyticsDataset.Samples,
            Metric = AnalyticsTrendMetric.AttachmentsCount,
            Bucket = AnalyticsBucket.Hour,
            Aggregation = AnalyticsAggregation.Avg,
            GroupBy = AnalyticsDimension.SampleCountry,
        };

        var expectedSql = """
            WITH
            sample_snapshots AS (
                SELECT
                    sample_id,
                    argMax(occurred_at, version) AS updated_at,
                    argMax(address_country, version) AS sample_country,
                    argMax(attachments_count, version) AS attachments_count
                FROM sample_events_analytics
                WHERE occurred_at <= {to:DateTime}
                GROUP BY sample_id
            )
            SELECT
                toStartOfHour(updated_at) AS time,
                if(length(sample_country) = 0, 'Unknown', sample_country) AS series_key,
                avg(attachments_count) AS value
            FROM sample_snapshots
            WHERE updated_at >= {from:DateTime}
            GROUP BY time, series_key
            ORDER BY time, series_key
            """;

        // Act
        var actual = AnalyticsQueryBuilder.BuildTrend(request);
        output.WriteLine(actual.Sql);

        // Assert
        using var scope = new AssertionScope();
        actual.Title.Should().Be("Attachment trend");
        actual.ValueLabel.Should().Be("Attachments");
        actual.AppliedBucket.Should().Be(AnalyticsBucket.Hour);
        actual.GroupBy.Should().Be(AnalyticsDimension.SampleCountry);
        NormalizeSql(actual.Sql).Should().Be(NormalizeSql(expectedSql));
    }

    [Fact]
    public void BuildTrend_ForOrderUnitsByShippingCountry_ReturnsExpectedDefinitionAndSql()
    {
        // Arrange
        var request = new GetAnalyticsTrendQuery
        {
            From = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            To = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Dataset = AnalyticsDataset.Orders,
            Metric = AnalyticsTrendMetric.Units,
            Bucket = AnalyticsBucket.Week,
            Aggregation = AnalyticsAggregation.Max,
            GroupBy = AnalyticsDimension.ShippingCountry,
        };

        var expectedSql = """
            WITH
            order_snapshots AS (
                SELECT
                    sample2_id,
                    argMax(occurred_at, version) AS updated_at,
                    argMax(status, version) AS status,
                    argMax(customer_segment, version) AS customer_segment,
                    argMax(shipping_country, version) AS shipping_country,
                    argMax(order_total_amount, version) AS order_total_amount,
                    argMax(items_quantity_total, version) AS items_quantity_total
                FROM sample2_events_analytics
                WHERE occurred_at <= {to:DateTime}
                GROUP BY sample2_id
            )
            SELECT
                toStartOfWeek(updated_at) AS time,
                if(length(shipping_country) = 0, 'Unknown', shipping_country) AS series_key,
                max(items_quantity_total) AS value
            FROM order_snapshots
            WHERE updated_at >= {from:DateTime}
            GROUP BY time, series_key
            ORDER BY time, series_key
            """;

        // Act
        var actual = AnalyticsQueryBuilder.BuildTrend(request);
        output.WriteLine(actual.Sql);

        // Assert
        using var scope = new AssertionScope();
        actual.Title.Should().Be("Units over time");
        actual.ValueLabel.Should().Be("Units");
        actual.AppliedBucket.Should().Be(AnalyticsBucket.Week);
        actual.GroupBy.Should().Be(AnalyticsDimension.ShippingCountry);
        NormalizeSql(actual.Sql).Should().Be(NormalizeSql(expectedSql));
    }

    [Fact]
    public void BuildBreakdown_ForEventsByEventType_ReturnsExpectedDefinitionAndSql()
    {
        // Arrange
        var request = new GetAnalyticsBreakdownQuery
        {
            From = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            To = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            Dataset = AnalyticsDataset.Events,
            Metric = AnalyticsBreakdownMetric.UniqueEntities,
            Dimension = AnalyticsDimension.EventType,
            Top = 5,
        };

        var expectedSql = """
            SELECT
                event_type AS bucket_key,
                uniqExact(tuple(entity_type, entity_id)) AS value
            FROM integration_events_raw FINAL
            WHERE occurred_at >= {from:DateTime}
              AND occurred_at <= {to:DateTime}
            GROUP BY bucket_key
            ORDER BY value DESC, bucket_key ASC
            LIMIT {top:Int32}
            """;

        // Act
        var actual = AnalyticsQueryBuilder.BuildBreakdown(request);
        output.WriteLine(actual.Sql);

        // Assert
        using var scope = new AssertionScope();
        actual.Title.Should().Be("Activity by event type");
        actual.ValueLabel.Should().Be("Entities");
        actual.Dimension.Should().Be(AnalyticsDimension.EventType);
        NormalizeSql(actual.Sql).Should().Be(NormalizeSql(expectedSql));
    }

    [Fact]
    public void BuildBreakdown_ForSampleAttachmentCoverageByCountry_ReturnsExpectedDefinitionAndSql()
    {
        // Arrange
        var request = new GetAnalyticsBreakdownQuery
        {
            From = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            To = new DateTime(2026, 4, 30, 0, 0, 0, DateTimeKind.Utc),
            Dataset = AnalyticsDataset.Samples,
            Metric = AnalyticsBreakdownMetric.AttachmentCoverage,
            Dimension = AnalyticsDimension.SampleCountry,
            Top = 6,
        };

        var expectedSql = """
            WITH
            sample_snapshots AS (
                SELECT
                    sample_id,
                    argMax(occurred_at, version) AS updated_at,
                    argMax(address_country, version) AS sample_country,
                    argMax(attachments_count, version) AS attachments_count
                FROM sample_events_analytics
                WHERE occurred_at <= {to:DateTime}
                GROUP BY sample_id
            )
            SELECT
                if(length(sample_country) = 0, 'Unknown', sample_country) AS bucket_key,
                avg(if(attachments_count > 0, 100.0, 0.0)) AS value
            FROM sample_snapshots
            WHERE updated_at >= {from:DateTime}
            GROUP BY bucket_key
            ORDER BY value DESC, bucket_key ASC
            LIMIT {top:Int32}
            """;

        // Act
        var actual = AnalyticsQueryBuilder.BuildBreakdown(request);
        output.WriteLine(actual.Sql);

        // Assert
        using var scope = new AssertionScope();
        actual.Title.Should().Be("Attachment coverage by country");
        actual.ValueLabel.Should().Be("Coverage %");
        actual.Dimension.Should().Be(AnalyticsDimension.SampleCountry);
        NormalizeSql(actual.Sql).Should().Be(NormalizeSql(expectedSql));
    }

    [Fact]
    public void BuildBreakdown_ForOrderAverageValueByCustomerSegment_ReturnsExpectedDefinitionAndSql()
    {
        // Arrange
        var request = new GetAnalyticsBreakdownQuery
        {
            From = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            To = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Dataset = AnalyticsDataset.Orders,
            Metric = AnalyticsBreakdownMetric.AvgOrderValue,
            Dimension = AnalyticsDimension.CustomerSegment,
            Top = 10,
        };

        var expectedSql = """
            WITH
            order_snapshots AS (
                SELECT
                    sample2_id,
                    argMax(occurred_at, version) AS updated_at,
                    argMax(status, version) AS status,
                    argMax(customer_segment, version) AS customer_segment,
                    argMax(shipping_country, version) AS shipping_country,
                    argMax(order_total_amount, version) AS order_total_amount,
                    argMax(items_quantity_total, version) AS items_quantity_total
                FROM sample2_events_analytics
                WHERE occurred_at <= {to:DateTime}
                GROUP BY sample2_id
            )
            SELECT
                if(length(customer_segment) = 0, 'Unknown', customer_segment) AS bucket_key,
                avg(order_total_amount) AS value
            FROM order_snapshots
            WHERE updated_at >= {from:DateTime}
            GROUP BY bucket_key
            ORDER BY value DESC, bucket_key ASC
            LIMIT {top:Int32}
            """;

        // Act
        var actual = AnalyticsQueryBuilder.BuildBreakdown(request);
        output.WriteLine(actual.Sql);

        // Assert
        using var scope = new AssertionScope();
        actual.Title.Should().Be("Orders by customer segment");
        actual.ValueLabel.Should().Be("Avg order value");
        actual.Dimension.Should().Be(AnalyticsDimension.CustomerSegment);
        NormalizeSql(actual.Sql).Should().Be(NormalizeSql(expectedSql));
    }

    [Fact]
    public void BuildTrend_ForUnsupportedEventMetric_ThrowsArgumentException()
    {
        // Arrange
        var request = new GetAnalyticsTrendQuery
        {
            From = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            To = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            Dataset = AnalyticsDataset.Events,
            Metric = AnalyticsTrendMetric.OrderCount,
        };

        // Act
        Action act = () => AnalyticsQueryBuilder.BuildTrend(request);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Events dataset supports only EventCount and UniqueEntities trend metrics.");
    }

    [Fact]
    public void BuildBreakdown_ForUnsupportedSampleDimension_ThrowsArgumentException()
    {
        // Arrange
        var request = new GetAnalyticsBreakdownQuery
        {
            From = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            To = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            Dataset = AnalyticsDataset.Samples,
            Metric = AnalyticsBreakdownMetric.SampleCount,
            Dimension = AnalyticsDimension.Status,
        };

        // Act
        Action act = () => AnalyticsQueryBuilder.BuildBreakdown(request);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Samples breakdown supports only SampleCountry dimension.");
    }

    private static string NormalizeSql(string sql) => sql.ReplaceLineEndings("\n").Trim();
}