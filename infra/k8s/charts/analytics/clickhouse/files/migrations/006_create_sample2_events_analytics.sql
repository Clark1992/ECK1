CREATE TABLE IF NOT EXISTS sample2_events_analytics
(
  occurred_at DateTime,
  occurred_date Date,

  event_id UUID,
  event_type LowCardinality(String),

  entity_id UUID,
  entity_version Int32,

  sample2_id UUID,
  version Int32,
  status Int32,

  customer_id UUID,
  customer_email String,
  customer_segment LowCardinality(String),

  shipping_city LowCardinality(String),
  shipping_country LowCardinality(String),

  currency LowCardinality(String),
  line_items_count UInt32,
  items_quantity_total UInt32,
  order_total_amount Float64,
  tags_count UInt32
)
ENGINE = MergeTree
PARTITION BY occurred_date
ORDER BY (event_type, occurred_at, sample2_id, event_id);

CREATE MATERIALIZED VIEW IF NOT EXISTS sample2_events_mv
TO sample2_events_analytics
AS
SELECT
  occurred_at,
  toDate(occurred_at) AS occurred_date,

  event_id,
  event_type,

  entity_id,
  entity_version,

  toUUIDOrZero(JSONExtractString(payload, 'sample2Id')) AS sample2_id,
  toInt32(JSONExtractInt(payload, 'version')) AS version,
  toInt32(JSONExtractInt(payload, 'status')) AS status,

  toUUIDOrZero(JSONExtractString(payload, 'customer', 'customerId')) AS customer_id,
  JSONExtractString(payload, 'customer', 'email') AS customer_email,
  JSONExtractString(payload, 'customer', 'segment') AS customer_segment,

  JSONExtractString(payload, 'shippingAddress', 'city') AS shipping_city,
  JSONExtractString(payload, 'shippingAddress', 'country') AS shipping_country,

  if(
    length(JSONExtractArrayRaw(payload, 'lineItems')) = 0,
    '',
    JSONExtractString(arrayElement(JSONExtractArrayRaw(payload, 'lineItems'), 1), 'unitPrice', 'currency')
  ) AS currency,
  toUInt32(length(JSONExtractArrayRaw(payload, 'lineItems'))) AS line_items_count,
  toUInt32(arraySum(arrayMap(item -> toFloat64(JSONExtractInt(item, 'quantity')), JSONExtractArrayRaw(payload, 'lineItems')))) AS items_quantity_total,
  arraySum(arrayMap(item -> JSONExtractFloat(item, 'unitPrice', 'amount') * toFloat64(JSONExtractInt(item, 'quantity')), JSONExtractArrayRaw(payload, 'lineItems'))) AS order_total_amount,
  toUInt32(length(JSONExtractArrayRaw(payload, 'tags'))) AS tags_count
FROM integration_events_raw
WHERE entity_type IN ('ECK1.Sample2', 'Sample2');

INSERT INTO sample2_events_analytics
SELECT
  occurred_at,
  toDate(occurred_at) AS occurred_date,

  event_id,
  event_type,

  entity_id,
  entity_version,

  toUUIDOrZero(JSONExtractString(payload, 'sample2Id')) AS sample2_id,
  toInt32(JSONExtractInt(payload, 'version')) AS version,
  toInt32(JSONExtractInt(payload, 'status')) AS status,

  toUUIDOrZero(JSONExtractString(payload, 'customer', 'customerId')) AS customer_id,
  JSONExtractString(payload, 'customer', 'email') AS customer_email,
  JSONExtractString(payload, 'customer', 'segment') AS customer_segment,

  JSONExtractString(payload, 'shippingAddress', 'city') AS shipping_city,
  JSONExtractString(payload, 'shippingAddress', 'country') AS shipping_country,

  if(
    length(JSONExtractArrayRaw(payload, 'lineItems')) = 0,
    '',
    JSONExtractString(arrayElement(JSONExtractArrayRaw(payload, 'lineItems'), 1), 'unitPrice', 'currency')
  ) AS currency,
  toUInt32(length(JSONExtractArrayRaw(payload, 'lineItems'))) AS line_items_count,
  toUInt32(arraySum(arrayMap(item -> toFloat64(JSONExtractInt(item, 'quantity')), JSONExtractArrayRaw(payload, 'lineItems')))) AS items_quantity_total,
  arraySum(arrayMap(item -> JSONExtractFloat(item, 'unitPrice', 'amount') * toFloat64(JSONExtractInt(item, 'quantity')), JSONExtractArrayRaw(payload, 'lineItems'))) AS order_total_amount,
  toUInt32(length(JSONExtractArrayRaw(payload, 'tags'))) AS tags_count
FROM integration_events_raw FINAL
WHERE entity_type IN ('ECK1.Sample2', 'Sample2');