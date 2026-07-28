# Project context (ECK1)

This file provides quick repository orientation and implementation context for people and coding agents. Treat source files and configuration as authoritative if this document becomes stale.

## Project structure

```
src/
  ECK1.CommandsAPI/       # CQRS command side — Orleans grains, Kafka consumers, MediatR handlers
  ECK1.QueriesAPI/        # CQRS query side — Mongo views + Elasticsearch search
  ECK1.Gateway/           # API gateway (YARP reverse proxy, Swagger aggregation, service discovery)
  ECK1.FE/                # Frontend (TBD)
  Deploy/                  # Repository deployment assets
  Integration/
    ECK1.FailedViewRebuilder/  # Rebuilds failed Mongo views
    ECK1.Integration.Cache.*/  # Short-term and long-term caching layers
    ECK1.Integration.Common/   # Shared integration utilities
    ECK1.Integration.Proxy/     # Receives integration records, routes to plugins
    ECK1.Reconciler/           # Version reconciliation service (SQL Server state, Kafka messaging)
    ECK1.VersionTracker/       # Version-tracking service
    IntegrationProxyPlugins/   # Mongo, Elasticsearch, Clickhouse plugins
    CodeGen/                   # Roslyn source generators for gRPC services
    common/                    # Shared integration build or support assets
  Nuget/
    ECK1.AsyncApi/             # Attributes: [Command], [Topic], [Route], [From*]
    ECK1.AsyncApi.CodeGen/     # Roslyn source generator — scans [Command]+[Topic], generates CommandConfigurator
    ECK1.CommonUtils/          # Shared utils (AutoMapper MapByInterface, TypeUtils)
    ECK1.Contracts/            # Business contracts (events, system records) — depends on Newtonsoft
    ECK1.Contracts.Shared/     # Shared code compiled into ECK1.Contracts (Polymorph<T> JSON converter)
    ECK1.Integration.Config/   # Integration manifest config loader
    ECK1.IntegrationContracts/ # Integration record types (ThinEvent, SampleFullRecord, etc.)
    ECK1.Kafka/                # Kafka consumer/producer infrastructure
    ECK1.Kafka.ProtoBuf/       # Protobuf serialization for Kafka
    ECK1.Notification.Contracts/ # Notification contracts
    ECK1.Orleans/              # Orleans abstractions (IGrainKeyResolver, IGrainRouter, IValueId)
    ECK1.RealtimeFeedback.Contracts/ # Realtime feedback contracts
    ECK1.Reconciliation.Contracts/ # POCO reconciliation types (RebuildRequest, ReconcileRequest, ReconcileResult)
    ECK1.VersionTracker.Contracts/ # Version-tracking contracts
tests/
  ECK1.CommandsAPI.Tests/
  ECK1.CommonUtils.Tests/
  ECK1.E2E.Tests/
  ECK1.Integration.Cache.ShortTerm.Tests/
  ECK1.Integration.Proxy.Tests/
  ECK1.QueriesAPI.Tests/
  ECK1.TestPlatform/
  ECK1.Tests.Common/
```

## Commands API — Kafka command flow

### How commands are consumed from Kafka

1. **Source generator** (`ECK1.AsyncApi.CodeGen/CommandRegistrationGenerator`):
   - Scans interfaces with `[Command]` + `[Topic]` attributes in the CommandsAPI project
   - Generates `CommandConfigurator.AddCommands(AbstractCommandConfigurator config)` which calls `config.RegisterCommand<TCmd>(topicConfigKey, topic)` for each command interface

2. **CommandConsumerConfig** implements `AbstractCommandConfigurator.RegisterCommand<TCmd>()`:
   - Registers `IKafkaMessageHandler<TCmd>` → `OrleansAdapter<TCmd, NullGrainMetadata, ICommandResult>`
   - Sets up a Kafka JSON consumer for `TCmd` on the resolved topic
   - Kafka deserializes JSON directly into the internal command type (uses STJ `[JsonPolymorphic]`/`[JsonDerivedType]` on the interface)

3. **OrleansAdapter** receives the deserialized command, calls `IGrainRouter.RouteToGrain()`

4. **CommandGrainHandler** wraps the command into `CommandRequest<TCmd, TState>` and sends via MediatR

### Internal command types (in `ECK1.CommandsAPI/Commands/`)

Internal command interfaces carry all infrastructure attributes:
- `[Command]`, `[Topic]` — for source generator discovery
- `[JsonPolymorphic]`/`[JsonDerivedType]` — STJ polymorphic deserialization (used by Kafka consumer)
- `[Newtonsoft.Json.JsonConverter(typeof(Polymorph<>))]` — Newtonsoft polymorphic support
- `[GenerateSerializer]` — Orleans serialization on concrete types
- `[Route]`, `[FromRoute]`, `[FromQuery]` — AsyncApi HTTP routing metadata
- Implement `IGrainKeyResolver<TState>`, `IRequest<(ICommandResult, TState)>`, `IValueId<Guid>`

Example: `ISampleCommand : IGrainKeyResolver<Sample>, IRequest<(ICommandResult, Sample)>`

### Kafka topics for commands
- `Kafka:SampleCommandsTopic` → `ISampleCommand`
- `Kafka:Sample2CommandsTopic` → `ISample2Command`

## Reconciliation subsystem

### ECK1.Reconciler (src/Integration/ECK1.Reconciler)
- ASP.NET Web API service with SQL Server state persistence (EF Core)
- Periodically checks entity version consistency across integration targets
- References: `ECK1.Contracts`, `ECK1.Reconciliation.Contracts`, `ECK1.Integration.Config`, `ECK1.IntegrationContracts`, `ECK1.Kafka`

### ECK1.Reconciliation.Contracts (src/Nuget/ECK1.Reconciliation.Contracts)
- Pure POCO nuget (zero external dependencies)
- Namespace: `ECK1.Reconciliation.Contracts`
- Types:
  - `ReconcileRequest` — contains `List<ReconcileRequestItem>` (EntityId, EntityType, ExpectedVersion)
  - `ReconcileResult` — EntityId, EntityType, FailedPlugin, IsFullHistoryRebuild
  - `RebuildRequest` — EntityId, EntityType, FailedTargets[], IsFullHistoryRebuild
- Consumed by: CommandsAPI, Reconciler, Integration.Proxy

### Reconciliation Kafka flow
- Reconciler sends `ReconcileRequest` → Integration.Proxy checks versions → returns `ReconcileResult`
- If mismatch: Reconciler sends `RebuildRequest` → CommandsAPI handles rebuild via `RebuildHandler<TCmd>`

## Queries API (src/ECK1.QueriesAPI)

### Mongo-backed endpoints
- `GET /api/samples/{id}` and `GET /api/samples` (paged)
- `GET /api/sample2s/{id}` and `GET /api/sample2s` (paged)
- Paging: `Skip`, `Top`, `Order` (e.g. `-name` for desc)
- Response: `PagedResponse<T> { Items, Total }`
- Mongo collections: `samples` → `SampleView`, `sample2s` → `Sample2View`

### Elasticsearch search endpoints
- `GET /search/samples` — full-text + filters over ES index `sample-full-records`
- `GET /search/sample2s` — full-text + filters over ES index `sample2-full-records`

Sample search filters:
- Full-text `q`, `HasAttachments`, `HasAddress`, exact `Countries`/`Cities`/`Streets`
- Sort: `name`, `description`, `address.street|city|country`

Sample2 search filters:
- Full-text `q`, `HasCustomer`, `HasShippingAddress`, `HasLineItems`
- Exact `Tags`, `ExcludeTags`, `Statuses`
- Nested amount: `LineItemUnitPriceAmountGt/Lt`, `HasLineItemUnitPriceAmountGt/Lt`
- Sort: `sample2Id`, `customer.email`, `customer.segment`, `shippingAddress.*`, `status`

## Integration Proxy (src/Integration/ECK1.Integration.Proxy + plugins)
- Receives integration messages, routes to plugins (Elasticsearch, Mongo, Clickhouse)
- Elasticsearch plugin: reads `ElasticSearchConfig:ClusterUrl` + `ElasticSearchConfig:ApiKey`, CA cert at `/etc/elasticsearch/certs/ca.crt`
- Index names from integration manifest ConfigMap

## Elasticsearch indices & mappings

- Sample: `sample-full-records`
  - `attachments` is `nested` (`fileName`: text, `url`: keyword)
- Sample2: `sample2-full-records`
  - `lineItems` is `nested` (`unitPrice.amount`: double)
  - `tags` is `nested` (`value`: text + `.keyword`)

Mapping files:
- `src/ECK1.CommandsAPI/Deploy/integration-manifests/files/elasticsearch/schemas/sample/sample-full-records.json`
- `src/ECK1.CommandsAPI/Deploy/integration-manifests/files/elasticsearch/schemas/sample2/sample2-full-records.json`

## Shared utilities (ECK1.CommonUtils)

### AutoMapper helpers (`ECK1.CommonUtils.Mapping`)
- `MapByInterface<TSrc, TDst>` — AutoMapper Profile that auto-maps all concrete implementations by matching class names, including nested property types
- `TypeUtils.GetTypesMappingWithNested<TSrc, TDst>()` — returns `Dictionary<Type, Type>` of source→dest mappings by name matching with recursive nested property discovery
- `MappingByNameBootstrapper<TSrc, TDst>` — base class for runtime type dispatch using name-matched mappings

## NuGet CI workflows

Each nuget has a publish workflow (`publish-nuget.*.yml`) that triggers `publish-nuget-shared.yml`:
- Triggers on push to `main` when package source changes
- Builds, packs, pushes to GitHub Packages

Kafka schema generation (`update-kafka-schemas.contracts.yml`):
- Triggered by: `Publish ECK1.Contracts`, `Publish ECK1.IntegrationContracts`, `Publish ECK1.Reconciliation.Contracts`
- Downloads latest nuget DLLs, runs schema generator for each topic
- Assembly path placeholders: `__BUSINESS_CONTRACTS_ASSEMBLY_PATH__`, `__INTEGRATION_CONTRACTS_ASSEMBLY_PATH__`, `__RECONCILIATION_CONTRACTS_ASSEMBLY_PATH__`
- Schema formats: JSON (commands, reconciliation), AVRO (thin events), Proto (full records)

## Helm / config wiring

Global vars ConfigMap contains:
- `ElasticSearchConfig__ClusterUrl`, `ElasticSearchConfig__CaSecretName`, `ElasticSearchConfig__ApiKeySecretName`

Queries API Helm deployment:
- Mounts CA secret at `/etc/elasticsearch/certs` when provided
- Provides `ElasticSearchConfig__ApiKey` from secret

