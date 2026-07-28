# Add New Entity Playbook

Use this as an agent-agnostic checklist when adding a new entity end-to-end in this repository. It is documentation, not a provider-specific slash command or prompt format.

## Goal

Add a new entity (e.g., `Sample3`) end-to-end across:

- CommandsAPI (write side)
- Kafka topics + Schema Registry artifacts
- ViewProjector (consumers + projections + thin/full outputs)
- QueriesAPI (read side endpoints)
- FailedViewRebuilder (DLQ ingestion + rebuild support)
- Integration artifacts (Elastic schema + integration manifest / ClickHouse mapping)
- TestPlatform (load scenarios)

## Hard rules

- Preserve existing conventions and naming patterns.
- Do not invent new fields: base everything on the entity’s contracts and the ES schema.
- Integration manifest correctness is strict:
  - Manifest fields must match indexed ES fields.
  - ClickHouse mapping must reference real payload fields.
- Do not use partial factories in TestPlatform; keep separate factories per entity.
- Build must succeed for: solution + TestPlatform.

## Step-by-step checklist

### 1) Contracts first

- Add entity contracts to `src/Nuget/ECK1.Contracts`.
- Add integration record contracts to `src/Nuget/ECK1.IntegrationContracts`:
  - Protobuf “full record” types
  - Avro “thin event” schema (ensure no duplicate JSON definitions)
- If codegen has limitations (e.g., list-of-primitive vs list-of-object), adjust types early and keep it consistent everywhere.

### 2) Infra: topics + schemas

- Add Kafka topic configuration under `infra/` (follow existing topic patterns).
- Add Schema Registry artifacts (Avro / JSON schema) alongside existing Sample/Sample2.
- Ensure topic names, schema subject names, and service configs line up.

### 3) CommandsAPI (write side)

- Add controller endpoints under `src/ECK1.CommandsAPI/Controllers`.
- Add command handlers + domain events.
- Ensure event payload patterns match existing (including `$type` handling if used).
- Add persistence/migrations if required.

### 4) ViewProjector (read model builder)

- Add Kafka consumers for the new entity’s events.
- Update mappings to:
  - build MongoDB view documents
  - produce thin/full events if required
- Ensure failures go to DLQ via the unified failure handler pattern.

### 5) QueriesAPI (read side)

- Add view model.
- Add query handlers.
- Add controller endpoints.

### 6) FailedViewRebuilder

- Ensure failures ingestion supports the new entity discriminator.
- Ensure rebuild endpoints and producer logic support the new entity.

### 7) Integration artifacts

- Add/update ElasticSearch schema JSON for the new entity’s indexed view fields.
- Update integration manifest:
  - confirm every field listed exists in the ES schema
  - confirm each ClickHouse mapping references actual payload fields

### 8) TestPlatform load scenarios

- Add `CommandsApiClient` methods for the new entity endpoints.
- Add a new fake data factory class (separate file/class, not partial).
- Add load endpoints in TestPlatform controllers.
- For create+update scenarios:
  - use `InterleavedCreateUpdateRunner` (single entity)
  - use `InterleavedTwoPoolCreateUpdateRunner` (two-entity mixed)

### 9) Verify

- `dotnet build src/ECK1.sln -c Release`
- `dotnet build tests/ECK1.TestPlatform/ECK1.TestPlatform.csproj -c Release`

Optionally run a manual smoke test for the new TestPlatform endpoints.

## Quick context (what already exists)

- Entities: `Sample`, `Sample2` implemented end-to-end.
- Load shaping: `min_rate`, `max_rate`, `rate_change_sec` supported by `LoadRunner`.
- Interleaved create+update helper: `InterleavedCreateUpdateRunner` in TestPlatform.

## Deliverables expected

- Code changes across services listed above.
- Updated schemas/topics/manifests.
- Updated TestPlatform endpoints.
- Solution builds cleanly.
