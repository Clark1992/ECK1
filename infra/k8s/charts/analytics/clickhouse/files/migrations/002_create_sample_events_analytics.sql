CREATE TABLE IF NOT EXISTS sample_events_analytics
(
  occurred_at DateTime,
  occurred_date Date,

  event_id UUID,
  event_type LowCardinality(String),

  entity_id UUID,
  entity_version Int32,

  sample_id UUID,
  name String,
  version Int32,

  address_city LowCardinality(String),
  address_country LowCardinality(String),

  attachments_count UInt32
)
ENGINE = MergeTree
PARTITION BY occurred_date
ORDER BY (event_type, occurred_at, sample_id, event_id);

CREATE MATERIALIZED VIEW IF NOT EXISTS sample_events_mv
TO sample_events_analytics
AS
SELECT
  occurred_at,
  toDate(occurred_at) AS occurred_date,

  event_id,
  event_type,

  entity_id,
  entity_version,

  toUUID(JSONExtractString(payload, 'sample_id')) AS sample_id,
  JSONExtractString(payload, 'name') AS name,
  toInt32(JSONExtractInt(payload, 'version')) AS version,

  JSONExtractString(payload, 'address_city') AS address_city,
  JSONExtractString(payload, 'address_country') AS address_country,

  toUInt32(length(JSONExtractArrayRaw(payload, 'attachments'))) AS attachments_count
FROM integration_events_raw
WHERE entity_type = 'Sample';
