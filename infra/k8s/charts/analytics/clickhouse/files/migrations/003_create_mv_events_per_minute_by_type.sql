CREATE MATERIALIZED VIEW IF NOT EXISTS events_per_minute_by_type_by_entity_type
ENGINE = SummingMergeTree
ORDER BY (event_type, minute)
AS
SELECT
    entity_type,
    event_type,
    toStartOfMinute(occurred_at) AS minute,
    count() AS events_count
FROM integration_events_raw
GROUP BY
    entity_type,
    event_type,
    minute;
