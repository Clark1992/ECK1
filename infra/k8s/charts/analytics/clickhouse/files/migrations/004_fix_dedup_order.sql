-- Step 1: Rename old table
RENAME TABLE integration_events_raw TO integration_events_raw_old;

-- Step 2: Create new table with correct ORDER BY
CREATE TABLE integration_events_raw
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
ORDER BY (entity_type, entity_id, entity_version);

-- Step 3: Copy data from old table
INSERT INTO integration_events_raw
SELECT * FROM integration_events_raw_old;

-- Step 4: Drop old table
DROP TABLE integration_events_raw_old;
