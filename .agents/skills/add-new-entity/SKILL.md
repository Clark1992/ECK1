---
name: add-new-entity
description: Add a new ECK1 business entity end to end across contracts, Kafka and Schema Registry artifacts, CommandsAPI, integration delivery, QueriesAPI, failed-view rebuilding, and TestPlatform scenarios. Use when creating an entity such as Sample3, extending an entity through the complete CQRS and integration pipeline, or auditing whether an entity is wired through every required service and deployment artifact.
---

# Add New Entity

Add one business entity through the repository's existing end-to-end patterns. Use `Sample` or `Sample2`, whichever is structurally closer, as the reference implementation.

## Establish the contract

1. Read the repository-root `AGENTS.md` and `docs/project-context.md`.
2. Obtain the entity fields, commands, events, identifiers, and required integrations from the user or an authoritative contract.
3. Stop and ask for the missing contract details if they are not available. Do not invent fields, event shapes, indexed properties, or analytics mappings.
4. Confirm whether frontend and analytics work are in scope. Treat them as explicit deliverables only when requested or required by the supplied contract.
5. Trace the closest existing entity through the repository before editing. Search for its type names, topics, schemas, registrations, migrations, manifests, endpoints, and load scenarios.

## Preserve these invariants

- Follow existing naming, namespace, serialization, routing, and registration patterns.
- Keep command topics, integration topics, schema subjects, service configuration, and manifest entries consistent.
- Keep contract, full-record, Mongo view, Elasticsearch mapping, and query field names aligned.
- Reference only real payload fields in integration and ClickHouse mappings.
- Use the repository's generation workflow for generated Avro, Protobuf, JSON-schema, or source-generated artifacts. Do not hand-edit generated output when an established generator owns it.
- Keep a separate TestPlatform fake-data factory for the entity; do not introduce partial factories.
- Avoid unrelated refactors while adding the entity.

## Implement the workflow

### 1. Add contracts

- Add business event contracts under `src/Nuget/ECK1.Contracts`.
- Add integration records under `src/Nuget/ECK1.IntegrationContracts`.
- Define the full record and the entity-specific thin-event type according to the existing serialization patterns.
- Resolve code-generation constraints early, especially collection and nested-object shapes, and use the selected representation consistently everywhere.

### 2. Add Kafka topics and schemas

- Add command, thin-event, full-record, and rebuild topics under `infra/k8s/charts/kafka/topic/topic-configs` as required by the closest reference entity.
- Add or generate the matching JSON Schema, Avro, and Protobuf artifacts.
- Verify topic names, schema subjects, record types, and application configuration keys against one another.
- Update schema-generation workflow inputs when the existing automation requires an explicit type or assembly entry.

### 3. Add the CommandsAPI write side

- Add domain state, domain events, DTOs, commands, and handlers under `src/ECK1.CommandsAPI`.
- Add controller endpoints and preserve existing route, query, and polymorphic-serialization conventions.
- Add event-store models, mappings, persistence registrations, and SQL migrations when the entity requires them.
- Add aggregate-to-full-record mapping.
- Register command handling, integration sending, serialization, and related services in the same locations as the reference entity.
- Ensure CommandsAPI emits the current thin/full integration flow; do not recreate the obsolete standalone ViewProjector architecture.

### 4. Add integration delivery and storage artifacts

- Add an entity entry to the integration manifest under `src/ECK1.CommandsAPI/Deploy/integration-manifests`.
- Configure event and record topics, record type, targets, event mappings, and target-specific field mappings.
- Add the Elasticsearch schema for every indexed field.
- Add Mongo migrations or mappings when the read model needs a new collection or shape.
- Add ClickHouse mappings or migrations only when analytics is in scope.
- Confirm every manifest field exists in its source payload and every Elasticsearch field exists in the schema.

### 5. Add the QueriesAPI read side

- Add the Mongo-backed view model and query handlers.
- Add list, get-by-id, search, history, or analytics handlers only where the entity contract requires them.
- Add controller endpoints and preserve the repository's paging, filtering, sorting, and response conventions.
- Register any entity-specific mappings or services required by the query path.

### 6. Check reconciliation and failed-view rebuilding

- Trace how the integration manifest drives `ECK1.Reconciler`, `ECK1.Integration.Proxy`, and `ECK1.FailedViewRebuilder`.
- Add explicit entity registrations, discriminators, rebuild topics, producers, or mappings only where the current implementation requires them.
- Verify that integration failures reach the shared DLQ flow and that rebuild requests can route back to the new entity.

### 7. Add TestPlatform coverage

- Add `CommandsApiClient` methods for the new endpoints.
- Add a dedicated fake-data factory file and class.
- Add operations, handlers, and load-controller endpoints following the closest existing entity.
- Use `InterleavedCreateUpdateRunner` for single-entity create/update scenarios.
- Use `InterleavedTwoPoolCreateUpdateRunner` for mixed two-entity scenarios.
- Keep load-shaping parameters consistent with `LoadRunner`.

### 8. Add optional frontend support

- When frontend work is in scope, add entity types, API clients, routes, list/detail pages, and forms under `src/ECK1.FE`.
- Derive validation and displayed fields from the same authoritative contract; do not infer additional fields from UI convenience.

## Verify completeness

1. Search for the reference entity and compare every relevant occurrence with the new entity.
2. Check for mismatches among type names, topic names, schema subjects, record types, route names, collection/index names, and manifest fields.
3. Build the solution:

   ```powershell
   dotnet build src/ECK1.sln -c Release
   ```

4. Build TestPlatform when it changed:

   ```powershell
   dotnet build tests/ECK1.TestPlatform/ECK1.TestPlatform.csproj -c Release
   ```

5. Do not run tests unless the user explicitly requests them.
6. Report completed surfaces, intentionally omitted optional surfaces, generated artifacts, build results, and any remaining manual deployment or smoke-test steps.
