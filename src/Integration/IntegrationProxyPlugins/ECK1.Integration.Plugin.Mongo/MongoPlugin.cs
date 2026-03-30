using ECK1.Integration.Plugin.Abstractions;
using ECK1.Integration.Plugin.Abstractions.ProjectionCompiler;
using ECK1.Integration.Config;
using ECK1.CommonUtils.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using OpenTelemetry.Trace;
using System.Collections.Concurrent;
using Generated = ECK1.IntegrationContracts.Kafka.IntegrationRecords.Generated;

namespace ECK1.Integration.Plugin.Mongo;

public class MongoPluginLoader : IIntergationPluginLoader
{
    public void Setup(IServiceCollection services, IConfiguration config, IntegrationConfig integrationConfig)
    {
        BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        services.AddSingleton(integrationConfig);
        services.AddSingleton(typeof(IIntergationPlugin<,>), typeof(MongoWriter<,>));
        services.AddSingleton<IReconciliationPlugin, MongoReconciliationPlugin>();
        services.AddSingleton(new MongoConnectionFactory(config));
    }

    public void SetupTelemetry(TracerProviderBuilder tracing)
    {
        // MongoDB instrumentation is set up via MongoClientSettings
    }
}

public sealed class MongoConnectionFactory
{
    private readonly ConcurrentDictionary<string, IMongoDatabase> databases = new();
    private readonly Dictionary<string, MongoConnectionConfig> connections;

    public MongoConnectionFactory(IConfiguration config)
    {
        connections = config.GetSection("MongoConnections")
            .GetChildren()
            .ToDictionary(
                s => s.Key,
                s => s.Get<MongoConnectionConfig>()
                    ?? throw new InvalidOperationException($"Invalid MongoConnections:{s.Key} config"));
    }

    public IMongoDatabase GetDatabase(string connectionName)
    {
        return databases.GetOrAdd(connectionName, name =>
        {
            if (!connections.TryGetValue(name, out var conn))
                throw new InvalidOperationException(
                    $"No MongoConnection named '{name}' configured. Available: [{string.Join(", ", connections.Keys)}]");

            if (string.IsNullOrWhiteSpace(conn.ConnectionString))
                throw new InvalidOperationException($"MongoConnections:{name}:ConnectionString is missing");

            if (string.IsNullOrWhiteSpace(conn.DatabaseName))
                throw new InvalidOperationException($"MongoConnections:{name}:DatabaseName is missing");

            var settings = MongoClientSettings
                .FromConnectionString(conn.ConnectionString)
                .AddOpenTelemetryInstrumentation();

            var client = new MongoClient(settings);
            return client.GetDatabase(conn.DatabaseName);
        });
    }
}

public class MongoWriter<TEvent, TMessage> : IIntergationPlugin<TEvent, TMessage>
    where TEvent : Generated.ThinEvent
{
    private readonly ILogger<MongoWriter<TEvent, TMessage>> logger;
    private readonly IMongoCollection<BsonDocument> collection;
    private readonly string entityIdField;
    private readonly BsonProjectionPlan<TMessage> plan;
    private readonly EventFieldExtractor eventFieldExtractor;

    public MongoWriter(
        ILogger<MongoWriter<TEvent, TMessage>> logger,
        MongoConnectionFactory connectionFactory,
        IntegrationConfig integrationConfig)
    {
        this.logger = logger;

        string messageType = typeof(TMessage).FullName;

        if (!integrationConfig.TryGetValue(messageType, out var entry))
            throw new InvalidOperationException($"Missing plugin config for {messageType}");

        var pluginConfig = entry.PluginConfig;

        var connectionName = pluginConfig["Connection"]
            ?? throw new InvalidOperationException($"Mongo:Connection is missing for {messageType}");

        var collectionName = pluginConfig["Collection"]
            ?? throw new InvalidOperationException($"Mongo:Collection is missing for {messageType}");

        this.entityIdField = pluginConfig["EntityIdField"]
            ?? throw new InvalidOperationException($"Mongo:EntityIdField is missing for {messageType}");

        var fields = pluginConfig.GetSection("Fields").Get<List<string>>()
            ?? throw new InvalidOperationException($"Mongo:Fields is missing for {messageType}");

        this.plan = BsonProjectionPlan<TMessage>.Compile(fields);
        this.eventFieldExtractor = EventFieldExtractor.Compile(
            pluginConfig.GetSection("EventMappings"));

        var database = connectionFactory.GetDatabase(connectionName);
        this.collection = database.GetCollection<BsonDocument>(collectionName);

        this.logger.LogInformation(
            "MongoPlugin: loaded for connection '{connection}', collection '{collection}', entityIdField '{entityIdField}'",
            connectionName, collectionName, entityIdField);
    }

    public async Task PushAsync(TEvent @event, TMessage message)
    {
        try
        {
            BsonDocument document = plan.Project(message);

            if (eventFieldExtractor.HasFields)
            {
                foreach (var (fieldName, value) in eventFieldExtractor.Extract(@event))
                    document[fieldName] = BsonValueConverter.ToBsonValue(value);
            }

            if (!document.TryGetValue(entityIdField, out BsonValue entityIdValue))
                throw new InvalidOperationException(
                    $"Document does not contain entityIdField '{entityIdField}'. Ensure it's listed in Fields config.");

            var filter = Builders<BsonDocument>.Filter.Eq(entityIdField, entityIdValue);

            await collection.ReplaceOneAsync(
                filter,
                document,
                new ReplaceOptions { IsUpsert = true });

            logger.LogInformation("MongoPlugin: Upserted document with {field}={value}",
                entityIdField, entityIdValue);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during calling MongoDB");
        }
    }
}

public class MongoReconciliationPlugin(
    MongoConnectionFactory connectionFactory,
    ILogger<MongoReconciliationPlugin> logger,
    IntegrationConfig integrationConfig) : IReconciliationPlugin
{
    public async Task<ReconciliationCheckResult> CheckAsync(Guid entityId, string entityType, int expectedVersion, CancellationToken ct)
    {
        var entry = integrationConfig
            .FirstOrDefault(kvp => kvp.Value.EntityType == entityType);

        if (entry.Value is null)
            return ReconciliationCheckResult.Ok;

        var pluginConfig = entry.Value.PluginConfig;

        var connectionName = pluginConfig["Connection"];
        var collectionName = pluginConfig["Collection"];
        var entityIdField = pluginConfig["EntityIdField"];

        if (connectionName is null || collectionName is null || entityIdField is null)
            return ReconciliationCheckResult.Ok;

        var database = connectionFactory.GetDatabase(connectionName);
        var collection = database.GetCollection<BsonDocument>(collectionName);

        var filter = Builders<BsonDocument>.Filter.Eq(entityIdField, new BsonBinaryData(entityId, GuidRepresentation.Standard));
        var doc = await collection.Find(filter).FirstOrDefaultAsync(ct);

        if (doc is null)
        {
            logger.LogWarning("Mongo reconciliation: entity {EntityId} not found in {Collection}", entityId, collectionName);
            return ReconciliationCheckResult.NeedsLatest;
        }

        return CheckVersion(doc, expectedVersion, entityId, collectionName);
    }

    private ReconciliationCheckResult CheckVersion(BsonDocument doc, int expectedVersion, Guid entityId, string collectionName)
    {
        if (doc.TryGetValue("version", out BsonValue versionValue) &&
            versionValue.IsInt32 &&
            versionValue.AsInt32 < expectedVersion)
        {
            logger.LogWarning(
                "Mongo reconciliation: entity {EntityId} version mismatch in {Collection}. Expected {Expected}, got {Actual}",
                entityId, collectionName, expectedVersion, versionValue.AsInt32);
            return ReconciliationCheckResult.NeedsLatest;
        }

        return ReconciliationCheckResult.Ok;
    }
}
