CREATE TABLE IF NOT EXISTS integration_events_raw
(
  event_id UUID,
  event_type LowCardinality(String),
  occurred_at DateTime,
  occurred_date Date MATERIALIZED toDate(occurred_at),
  entity_id UUID,
  entity_type LowCardinality(String),
  entity_version Int32,
  payload String
)
ENGINE = ReplacingMergeTree
PARTITION BY occurred_date
ORDER BY (event_id);
