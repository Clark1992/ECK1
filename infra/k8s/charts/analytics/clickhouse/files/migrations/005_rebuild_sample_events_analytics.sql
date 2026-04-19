DROP VIEW IF EXISTS sample_events_mv;

TRUNCATE TABLE sample_events_analytics;

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

  toUUIDOrZero(JSONExtractString(payload, 'sample_id')) AS sample_id,
  JSONExtractString(payload, 'name') AS name,
  toInt32(JSONExtractInt(payload, 'version')) AS version,

  JSONExtractString(payload, 'address_city') AS address_city,
  JSONExtractString(payload, 'address_country') AS address_country,

  toUInt32(length(JSONExtractArrayRaw(payload, 'attachments'))) AS attachments_count
FROM integration_events_raw
WHERE entity_type IN ('ECK1.Sample', 'Sample');

INSERT INTO sample_events_analytics
SELECT
  occurred_at,
  toDate(occurred_at) AS occurred_date,

  event_id,
  event_type,

  entity_id,
  entity_version,

  toUUIDOrZero(JSONExtractString(payload, 'sample_id')) AS sample_id,
  JSONExtractString(payload, 'name') AS name,
  toInt32(JSONExtractInt(payload, 'version')) AS version,

  JSONExtractString(payload, 'address_city') AS address_city,
  JSONExtractString(payload, 'address_country') AS address_country,

  toUInt32(length(JSONExtractArrayRaw(payload, 'attachments'))) AS attachments_count
FROM integration_events_raw FINAL
WHERE entity_type IN ('ECK1.Sample', 'Sample');