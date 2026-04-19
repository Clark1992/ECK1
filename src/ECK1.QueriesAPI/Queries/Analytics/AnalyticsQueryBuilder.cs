namespace ECK1.QueriesAPI.Queries.Analytics;

internal static class AnalyticsQueryBuilder
{
    public static void ValidateRange(DateTime from, DateTime to)
    {
        if (from >= to)
        {
            throw new ArgumentException("'From' must be earlier than 'To'.");
        }
    }

    public static AnalyticsBucket ResolveBucket(DateTime from, DateTime to, AnalyticsBucket requestedBucket)
    {
        if (requestedBucket != AnalyticsBucket.Auto)
        {
            return requestedBucket;
        }

        var range = to - from;
        if (range.TotalDays > 90)
        {
            return AnalyticsBucket.Week;
        }

        if (range.TotalDays > 14)
        {
            return AnalyticsBucket.Day;
        }

        return AnalyticsBucket.Hour;
    }

    public static string BuildSampleSnapshotsCte() => """
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
        """;

    public static string BuildOrderSnapshotsCte() => """
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
        """;

    public static AnalyticsTrendDefinition BuildTrend(GetAnalyticsTrendQuery request)
    {
        ValidateRange(request.From, request.To);

        var appliedBucket = ResolveBucket(request.From, request.To, request.Bucket);
        var bucketExpression = GetBucketExpression(appliedBucket, "updated_at");

        return request.Dataset switch
        {
            AnalyticsDataset.Events => BuildEventsTrend(request, appliedBucket),
            AnalyticsDataset.Samples => BuildSamplesTrend(request, appliedBucket, bucketExpression),
            AnalyticsDataset.Orders => BuildOrdersTrend(request, appliedBucket, bucketExpression),
            _ => throw new ArgumentOutOfRangeException(nameof(request.Dataset), request.Dataset, null),
        };
    }

    public static AnalyticsBreakdownDefinition BuildBreakdown(GetAnalyticsBreakdownQuery request)
    {
        ValidateRange(request.From, request.To);

        return request.Dataset switch
        {
            AnalyticsDataset.Events => BuildEventsBreakdown(request),
            AnalyticsDataset.Samples => BuildSamplesBreakdown(request),
            AnalyticsDataset.Orders => BuildOrdersBreakdown(request),
            _ => throw new ArgumentOutOfRangeException(nameof(request.Dataset), request.Dataset, null),
        };
    }

    public static string FormatDimensionLabel(AnalyticsDimension? dimension, string rawValue)
    {
        if (dimension is null)
        {
            return string.IsNullOrWhiteSpace(rawValue) ? "Total" : rawValue;
        }

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return "Unknown";
        }

        return dimension.Value switch
        {
            AnalyticsDimension.EntityType => rawValue switch
            {
                "ECK1.Sample" => "Samples",
                "ECK1.Sample2" => "Orders",
                "Sample" => "Samples",
                "Sample2" => "Orders",
                _ => rawValue,
            },
            AnalyticsDimension.Status => rawValue switch
            {
                "0" => "Draft",
                "1" => "Submitted",
                "2" => "Paid",
                "3" => "Shipped",
                "4" => "Cancelled",
                _ => rawValue,
            },
            _ => rawValue,
        };
    }

    private static AnalyticsTrendDefinition BuildEventsTrend(GetAnalyticsTrendQuery request, AnalyticsBucket appliedBucket)
    {
        if (request.Metric is not (AnalyticsTrendMetric.EventCount or AnalyticsTrendMetric.UniqueEntities))
        {
            throw new ArgumentException("Events dataset supports only EventCount and UniqueEntities trend metrics.");
        }

        if (request.GroupBy is not (null or AnalyticsDimension.EntityType or AnalyticsDimension.EventType))
        {
            throw new ArgumentException("Events trend can be grouped only by EntityType or EventType.");
        }

        var bucketExpression = GetBucketExpression(appliedBucket, "occurred_at");
        var groupExpression = request.GroupBy switch
        {
            AnalyticsDimension.EntityType => "entity_type",
            AnalyticsDimension.EventType => "event_type",
            null => "'total'",
            _ => throw new ArgumentOutOfRangeException(nameof(request.GroupBy), request.GroupBy, null),
        };
        var metricExpression = request.Metric switch
        {
            AnalyticsTrendMetric.EventCount => "count()",
            AnalyticsTrendMetric.UniqueEntities => "uniqExact(tuple(entity_type, entity_id))",
            _ => throw new ArgumentOutOfRangeException(nameof(request.Metric), request.Metric, null),
        };
        var valueLabel = request.Metric switch
        {
            AnalyticsTrendMetric.EventCount => "Events",
            AnalyticsTrendMetric.UniqueEntities => "Entities",
            _ => throw new ArgumentOutOfRangeException(nameof(request.Metric), request.Metric, null),
        };

        var sql = $$"""
            SELECT
                {{bucketExpression}} AS time,
                {{groupExpression}} AS series_key,
                {{metricExpression}} AS value
            FROM integration_events_raw FINAL
            WHERE occurred_at >= {from:DateTime}
              AND occurred_at <= {to:DateTime}
            GROUP BY time, series_key
            ORDER BY time, series_key
            """;

        return new AnalyticsTrendDefinition(
            Title: request.Metric == AnalyticsTrendMetric.EventCount ? "Event flow" : "Unique entities over time",
            ValueLabel: valueLabel,
            AppliedBucket: appliedBucket,
            GroupBy: request.GroupBy,
            Sql: sql);
    }

    private static AnalyticsTrendDefinition BuildSamplesTrend(
        GetAnalyticsTrendQuery request,
        AnalyticsBucket appliedBucket,
        string bucketExpression)
    {
        if (request.Metric is not (AnalyticsTrendMetric.SampleCount or AnalyticsTrendMetric.AttachmentsCount))
        {
            throw new ArgumentException("Samples dataset supports only SampleCount and AttachmentsCount trend metrics.");
        }

        if (request.GroupBy is not (null or AnalyticsDimension.SampleCountry))
        {
            throw new ArgumentException("Samples trend can be grouped only by SampleCountry.");
        }

        var groupExpression = request.GroupBy switch
        {
            AnalyticsDimension.SampleCountry => "if(length(sample_country) = 0, 'Unknown', sample_country)",
            null => "'total'",
            _ => throw new ArgumentOutOfRangeException(nameof(request.GroupBy), request.GroupBy, null),
        };

        var metricExpression = request.Metric switch
        {
            AnalyticsTrendMetric.SampleCount => "count()",
            AnalyticsTrendMetric.AttachmentsCount => BuildAggregateExpression("attachments_count", request.Aggregation),
            _ => throw new ArgumentOutOfRangeException(nameof(request.Metric), request.Metric, null),
        };

        var valueLabel = request.Metric switch
        {
            AnalyticsTrendMetric.SampleCount => "Samples",
            AnalyticsTrendMetric.AttachmentsCount => "Attachments",
            _ => throw new ArgumentOutOfRangeException(nameof(request.Metric), request.Metric, null),
        };

        var sql = $$"""
            WITH
            {{BuildSampleSnapshotsCte()}}
            SELECT
                {{bucketExpression}} AS time,
                {{groupExpression}} AS series_key,
                {{metricExpression}} AS value
            FROM sample_snapshots
            WHERE updated_at >= {from:DateTime}
            GROUP BY time, series_key
            ORDER BY time, series_key
            """;

        return new AnalyticsTrendDefinition(
            Title: request.Metric == AnalyticsTrendMetric.SampleCount ? "Samples updated over time" : "Attachment trend",
            ValueLabel: valueLabel,
            AppliedBucket: appliedBucket,
            GroupBy: request.GroupBy,
            Sql: sql);
    }

    private static AnalyticsTrendDefinition BuildOrdersTrend(
        GetAnalyticsTrendQuery request,
        AnalyticsBucket appliedBucket,
        string bucketExpression)
    {
        if (request.Metric is not (AnalyticsTrendMetric.OrderCount or AnalyticsTrendMetric.OrderGrossValue or AnalyticsTrendMetric.Units))
        {
            throw new ArgumentException("Orders dataset supports only OrderCount, OrderGrossValue and Units trend metrics.");
        }

        if (request.GroupBy is not (null or AnalyticsDimension.Status or AnalyticsDimension.CustomerSegment or AnalyticsDimension.ShippingCountry))
        {
            throw new ArgumentException("Orders trend can be grouped only by Status, CustomerSegment or ShippingCountry.");
        }

        var groupExpression = request.GroupBy switch
        {
            AnalyticsDimension.Status => "toString(status)",
            AnalyticsDimension.CustomerSegment => "if(length(customer_segment) = 0, 'Unknown', customer_segment)",
            AnalyticsDimension.ShippingCountry => "if(length(shipping_country) = 0, 'Unknown', shipping_country)",
            null => "'total'",
            _ => throw new ArgumentOutOfRangeException(nameof(request.GroupBy), request.GroupBy, null),
        };

        var metricExpression = request.Metric switch
        {
            AnalyticsTrendMetric.OrderCount => "count()",
            AnalyticsTrendMetric.OrderGrossValue => BuildAggregateExpression("order_total_amount", request.Aggregation),
            AnalyticsTrendMetric.Units => BuildAggregateExpression("items_quantity_total", request.Aggregation),
            _ => throw new ArgumentOutOfRangeException(nameof(request.Metric), request.Metric, null),
        };

        var title = request.Metric switch
        {
            AnalyticsTrendMetric.OrderCount => "Orders updated over time",
            AnalyticsTrendMetric.OrderGrossValue => "Gross value over time",
            AnalyticsTrendMetric.Units => "Units over time",
            _ => throw new ArgumentOutOfRangeException(nameof(request.Metric), request.Metric, null),
        };

        var valueLabel = request.Metric switch
        {
            AnalyticsTrendMetric.OrderCount => "Orders",
            AnalyticsTrendMetric.OrderGrossValue => "Gross value",
            AnalyticsTrendMetric.Units => "Units",
            _ => throw new ArgumentOutOfRangeException(nameof(request.Metric), request.Metric, null),
        };

        var sql = $$"""
            WITH
            {{BuildOrderSnapshotsCte()}}
            SELECT
                {{bucketExpression}} AS time,
                {{groupExpression}} AS series_key,
                {{metricExpression}} AS value
            FROM order_snapshots
            WHERE updated_at >= {from:DateTime}
            GROUP BY time, series_key
            ORDER BY time, series_key
            """;

        return new AnalyticsTrendDefinition(
            Title: title,
            ValueLabel: valueLabel,
            AppliedBucket: appliedBucket,
            GroupBy: request.GroupBy,
            Sql: sql);
    }

    private static AnalyticsBreakdownDefinition BuildEventsBreakdown(GetAnalyticsBreakdownQuery request)
    {
        if (request.Dimension is not (AnalyticsDimension.EntityType or AnalyticsDimension.EventType))
        {
            throw new ArgumentException("Events breakdown supports only EntityType or EventType dimensions.");
        }

        if (request.Metric is not (AnalyticsBreakdownMetric.EventCount or AnalyticsBreakdownMetric.UniqueEntities))
        {
            throw new ArgumentException("Events breakdown supports only EventCount and UniqueEntities metrics.");
        }

        var dimensionExpression = request.Dimension switch
        {
            AnalyticsDimension.EntityType => "entity_type",
            AnalyticsDimension.EventType => "event_type",
            _ => throw new ArgumentOutOfRangeException(nameof(request.Dimension), request.Dimension, null),
        };
        var metricExpression = request.Metric switch
        {
            AnalyticsBreakdownMetric.EventCount => "count()",
            AnalyticsBreakdownMetric.UniqueEntities => "uniqExact(tuple(entity_type, entity_id))",
            _ => throw new ArgumentOutOfRangeException(nameof(request.Metric), request.Metric, null),
        };

        var sql = $$"""
            SELECT
                {{dimensionExpression}} AS bucket_key,
                {{metricExpression}} AS value
            FROM integration_events_raw FINAL
            WHERE occurred_at >= {from:DateTime}
              AND occurred_at <= {to:DateTime}
            GROUP BY bucket_key
            ORDER BY value DESC, bucket_key ASC
            LIMIT {top:Int32}
            """;

        return new AnalyticsBreakdownDefinition(
            Title: request.Dimension == AnalyticsDimension.EntityType ? "Activity by entity type" : "Activity by event type",
            ValueLabel: request.Metric == AnalyticsBreakdownMetric.EventCount ? "Events" : "Entities",
            Dimension: request.Dimension,
            Sql: sql);
    }

    private static AnalyticsBreakdownDefinition BuildSamplesBreakdown(GetAnalyticsBreakdownQuery request)
    {
        if (request.Dimension != AnalyticsDimension.SampleCountry)
        {
            throw new ArgumentException("Samples breakdown supports only SampleCountry dimension.");
        }

        if (request.Metric is not (AnalyticsBreakdownMetric.SampleCount or AnalyticsBreakdownMetric.AttachmentsCount or AnalyticsBreakdownMetric.AttachmentCoverage))
        {
            throw new ArgumentException("Samples breakdown supports SampleCount, AttachmentsCount and AttachmentCoverage metrics.");
        }

        var metricExpression = request.Metric switch
        {
            AnalyticsBreakdownMetric.SampleCount => "count()",
            AnalyticsBreakdownMetric.AttachmentsCount => BuildAggregateExpression("attachments_count", request.Aggregation),
            AnalyticsBreakdownMetric.AttachmentCoverage => "avg(if(attachments_count > 0, 100.0, 0.0))",
            _ => throw new ArgumentOutOfRangeException(nameof(request.Metric), request.Metric, null),
        };

        var sql = $$"""
            WITH
            {{BuildSampleSnapshotsCte()}}
            SELECT
                if(length(sample_country) = 0, 'Unknown', sample_country) AS bucket_key,
                {{metricExpression}} AS value
            FROM sample_snapshots
            WHERE updated_at >= {from:DateTime}
            GROUP BY bucket_key
            ORDER BY value DESC, bucket_key ASC
            LIMIT {top:Int32}
            """;

        return new AnalyticsBreakdownDefinition(
            Title: request.Metric == AnalyticsBreakdownMetric.AttachmentCoverage ? "Attachment coverage by country" : "Samples by country",
            ValueLabel: request.Metric switch
            {
                AnalyticsBreakdownMetric.SampleCount => "Samples",
                AnalyticsBreakdownMetric.AttachmentsCount => "Attachments",
                AnalyticsBreakdownMetric.AttachmentCoverage => "Coverage %",
                _ => throw new ArgumentOutOfRangeException(nameof(request.Metric), request.Metric, null),
            },
            Dimension: request.Dimension,
            Sql: sql);
    }

    private static AnalyticsBreakdownDefinition BuildOrdersBreakdown(GetAnalyticsBreakdownQuery request)
    {
        if (request.Dimension is not (AnalyticsDimension.Status or AnalyticsDimension.CustomerSegment or AnalyticsDimension.ShippingCountry))
        {
            throw new ArgumentException("Orders breakdown supports only Status, CustomerSegment and ShippingCountry dimensions.");
        }

        if (request.Metric is not (AnalyticsBreakdownMetric.OrderCount or AnalyticsBreakdownMetric.OrderGrossValue or AnalyticsBreakdownMetric.AvgOrderValue or AnalyticsBreakdownMetric.Units))
        {
            throw new ArgumentException("Orders breakdown supports OrderCount, OrderGrossValue, AvgOrderValue and Units metrics.");
        }

        var dimensionExpression = request.Dimension switch
        {
            AnalyticsDimension.Status => "toString(status)",
            AnalyticsDimension.CustomerSegment => "if(length(customer_segment) = 0, 'Unknown', customer_segment)",
            AnalyticsDimension.ShippingCountry => "if(length(shipping_country) = 0, 'Unknown', shipping_country)",
            _ => throw new ArgumentOutOfRangeException(nameof(request.Dimension), request.Dimension, null),
        };

        var metricExpression = request.Metric switch
        {
            AnalyticsBreakdownMetric.OrderCount => "count()",
            AnalyticsBreakdownMetric.OrderGrossValue => BuildAggregateExpression("order_total_amount", request.Aggregation),
            AnalyticsBreakdownMetric.AvgOrderValue => "avg(order_total_amount)",
            AnalyticsBreakdownMetric.Units => BuildAggregateExpression("items_quantity_total", request.Aggregation),
            _ => throw new ArgumentOutOfRangeException(nameof(request.Metric), request.Metric, null),
        };

        var title = request.Dimension switch
        {
            AnalyticsDimension.Status => "Orders by status",
            AnalyticsDimension.CustomerSegment => request.Metric == AnalyticsBreakdownMetric.OrderGrossValue ? "Gross value by customer segment" : "Orders by customer segment",
            AnalyticsDimension.ShippingCountry => "Orders by shipping country",
            _ => throw new ArgumentOutOfRangeException(nameof(request.Dimension), request.Dimension, null),
        };

        var valueLabel = request.Metric switch
        {
            AnalyticsBreakdownMetric.OrderCount => "Orders",
            AnalyticsBreakdownMetric.OrderGrossValue => "Gross value",
            AnalyticsBreakdownMetric.AvgOrderValue => "Avg order value",
            AnalyticsBreakdownMetric.Units => "Units",
            _ => throw new ArgumentOutOfRangeException(nameof(request.Metric), request.Metric, null),
        };

        var sql = $$"""
            WITH
            {{BuildOrderSnapshotsCte()}}
            SELECT
                {{dimensionExpression}} AS bucket_key,
                {{metricExpression}} AS value
            FROM order_snapshots
            WHERE updated_at >= {from:DateTime}
            GROUP BY bucket_key
            ORDER BY value DESC, bucket_key ASC
            LIMIT {top:Int32}
            """;

        return new AnalyticsBreakdownDefinition(
            Title: title,
            ValueLabel: valueLabel,
            Dimension: request.Dimension,
            Sql: sql);
    }

    private static string BuildAggregateExpression(string fieldName, AnalyticsAggregation aggregation) => aggregation switch
    {
        AnalyticsAggregation.Sum => $"sum({fieldName})",
        AnalyticsAggregation.Avg => $"avg({fieldName})",
        AnalyticsAggregation.Min => $"min({fieldName})",
        AnalyticsAggregation.Max => $"max({fieldName})",
        _ => throw new ArgumentOutOfRangeException(nameof(aggregation), aggregation, null),
    };

    private static string GetBucketExpression(AnalyticsBucket bucket, string fieldName) => bucket switch
    {
        AnalyticsBucket.Hour => $"toStartOfHour({fieldName})",
        AnalyticsBucket.Day => $"toStartOfDay({fieldName})",
        AnalyticsBucket.Week => $"toStartOfWeek({fieldName})",
        AnalyticsBucket.Auto => throw new ArgumentException("Auto bucket must be resolved before building SQL."),
        _ => throw new ArgumentOutOfRangeException(nameof(bucket), bucket, null),
    };
}