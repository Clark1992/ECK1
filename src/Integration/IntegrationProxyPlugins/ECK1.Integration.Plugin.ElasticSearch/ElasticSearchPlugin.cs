using ECK1.Integration.Plugin.Abstractions;
using ECK1.Integration.Plugin.Abstractions.ProjectionCompiler;
using ECK1.Integration.Config;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Nodes;
using ECK1.CommonUtils.OpenTelemetry;
using Generated = ECK1.IntegrationContracts.Kafka.IntegrationRecords.Generated;

namespace ECK1.Integration.Plugin.ElasticSearch;

public class ElasticSearchPluginLoader : IIntergationPluginLoader
{
    public void Setup(IServiceCollection services, IConfiguration config, IntegrationConfig integrationConfig)
    {
        services.Configure<ElasticSearchConfig>(config.GetSection(nameof(ElasticSearchConfig)));
        services.AddSingleton(integrationConfig);

        services.AddSingleton(typeof(IIntergationPlugin<,>), typeof(ElasticSearchWriter<,>));
        services.AddSingleton<IReconciliationPlugin, ElasticSearchReconciliationPlugin>();

        services.AddSingleton<ElasticsearchClient>(sp =>
        {
            var config = sp.GetRequiredService<IOptions<ElasticSearchConfig>>().Value;

            var caPath = "/etc/elasticsearch/certs/ca.crt";

            if (string.IsNullOrWhiteSpace(config.ClusterUrl))
                throw new Exception("ElasticsearchConfig:ClusterUrl is missing");

            if (string.IsNullOrWhiteSpace(config.ApiKey))
                throw new Exception("ElasticsearchConfig:ApiKey is missing");

            X509Certificate2 caCert = !string.IsNullOrEmpty(caPath) && File.Exists(caPath)
                ? new X509Certificate2(caPath)
                : throw new Exception($"CA certificate not found at {caPath}");

            var settings = new ElasticsearchClientSettings(new Uri(config.ClusterUrl))
                .Authentication(new ApiKey(config.ApiKey))
                .ServerCertificateValidationCallback((sender, cert, chain, errors) =>
                {
                    var chain2 = new X509Chain();
                    chain2.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

                    chain2.ChainPolicy.ExtraStore.Add(caCert);

                    chain2.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;

                    chain2.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                    chain2.ChainPolicy.CustomTrustStore.Add(caCert);

                    return chain2.Build((X509Certificate2)cert);
                });

            return new ElasticsearchClient(settings);
        });

    }

    public void SetupTelemetry(TracerProviderBuilder tracing)
    {
        tracing.AddElasticsearchInstrumentation();
    }
}

public class ElasticSearchWriter<TEvent, TMessage> : IIntergationPlugin<TEvent, TMessage>
    where TEvent : Generated.ThinEvent
{
    private readonly ElasticsearchClient client;
    private readonly ILogger<ElasticSearchWriter<TEvent, TMessage>> logger;
    private readonly List<string> indexes;
    private readonly ElasticSearchConfig config;
    private readonly EventFieldExtractor eventFieldExtractor;

    public ElasticSearchWriter(
        ElasticsearchClient client,
        ILogger<ElasticSearchWriter<TEvent, TMessage>> logger,
        IOptions<ElasticSearchConfig> options,
        IntegrationConfig integrationConfig)
    {
        this.logger = logger;
        string messageType = typeof(TMessage).FullName;
        this.indexes = integrationConfig.TryGetValue(
                messageType, out var entry) ?
            entry.PluginConfig.GetSection("Indexes").Get<List<string>>() :
            throw new InvalidOperationException($"Missing plugin config for {messageType}");

        this.eventFieldExtractor = EventFieldExtractor.Compile(
            entry.PluginConfig.GetSection("EventMappings"));

        this.config = options.Value;
        this.client = client;
        this.logger.LogInformation("ElasticSearchPlugin: loaded");
        this.logger.LogInformation("Config: {config}", JsonSerializer.Serialize(config));
        this.logger.LogInformation("Indexes for {type}: {config}", messageType, JsonSerializer.Serialize(this.indexes));
    }

    public async Task PushAsync(TEvent @event, TMessage message)
    {
        await Task.WhenAll([.. this.indexes.Select(async index =>
        {
            try
            {
                if (eventFieldExtractor.HasFields)
                {
                    var jsonNode = JsonSerializer.SerializeToNode(message);
                    if (jsonNode is JsonObject jsonObj)
                    {
                        foreach (var (fieldName, value) in eventFieldExtractor.Extract(@event))
                            jsonObj[fieldName] = JsonValue.Create(value);
                    }
                    var response = await client.IndexAsync(jsonNode, i => i.Index(index));
                    this.logger.LogInformation("Response from ElasticSearchPlugin: {res}", response);
                }
                else
                {
                    var response = await client.IndexAsync(message, i => i.Index(index));
                    this.logger.LogInformation("Response from ElasticSearchPlugin: {res}", response);
                }
            }
            catch (Exception e)
            {
                this.logger.LogError(e, "Error during calling ES");
            }
        })]);
    }
}

public class ElasticSearchReconciliationPlugin(
    ElasticsearchClient client,
    ILogger<ElasticSearchReconciliationPlugin> logger,
    IntegrationConfig integrationConfig) : IReconciliationPlugin
{
    public async Task<ReconciliationCheckResult> CheckAsync(Guid entityId, string entityType, int expectedVersion, CancellationToken ct)
    {
        var entry = integrationConfig
            .FirstOrDefault(kvp => kvp.Value.EntityType == entityType);

        if (entry.Value is null)
            return ReconciliationCheckResult.Ok;

        var indexes = entry.Value.PluginConfig.GetSection("Indexes").Get<List<string>>();
        if (indexes is null || indexes.Count == 0)
            return ReconciliationCheckResult.Ok;

        var index = indexes[0];

        var response = await client.GetAsync<JsonDocument>(index, entityId.ToString(), ct);

        if (!response.IsValidResponse || response.Source is null)
        {
            logger.LogWarning("ES reconciliation: entity {EntityId} not found in index {Index}", entityId, index);
            return ReconciliationCheckResult.NeedsLatest;
        }

        return CheckVersion(response.Source.RootElement, expectedVersion, entityId, index);
    }

    private ReconciliationCheckResult CheckVersion(JsonElement root, int expectedVersion, Guid entityId, string index)
    {
        if (root.TryGetProperty("version", out var versionElement) &&
            versionElement.TryGetInt32(out int storedVersion) &&
            storedVersion < expectedVersion)
        {
            logger.LogWarning(
                "ES reconciliation: entity {EntityId} version mismatch in {Index}. Expected {Expected}, got {Actual}",
                entityId, index, expectedVersion, storedVersion);
            return ReconciliationCheckResult.NeedsLatest;
        }

        return ReconciliationCheckResult.Ok;
    }
}
