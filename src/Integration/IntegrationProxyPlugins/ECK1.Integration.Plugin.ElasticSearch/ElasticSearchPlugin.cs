using ECK1.Integration.Plugin.Abstractions;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace ECK1.Integration.Plugin.ElasticSearch;

public class ElasticSearchPluginLoader : IIntergationPluginLoader
{
    public void Setup(IServiceCollection services, IConfiguration config, IntegrationConfig integrationConfig)
    {
        services.Configure<ElasticSearchConfig>(config.GetSection(nameof(ElasticSearchConfig)));
        services.AddSingleton(integrationConfig);

        services.AddSingleton(typeof(IIntergationPlugin<,>), typeof(ElasticSearchWriter<,>));

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
}

public class ElasticSearchWriter<TEvent, TMessage> : IIntergationPlugin<TEvent, TMessage>
{
    private readonly ElasticsearchClient client;
    private readonly ILogger<ElasticSearchWriter<TEvent, TMessage>> logger;
    private readonly List<string> indexes;
    private readonly ElasticSearchConfig config;

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

        this.config = options.Value;
        this.client = client;
        this.logger.LogInformation("ElasticSearchPlugin: loaded");
        this.logger.LogInformation("Config: {config}", JsonSerializer.Serialize(config));
        this.logger.LogInformation("Indexes for {type}: {config}", messageType, JsonSerializer.Serialize(this.indexes));
    }

    public async Task PushAsync(TEvent _, TMessage message)
    {
        try
        {
            await Task.WhenAll([.. this.indexes.Select(async index =>
            {
                var response = await client.IndexAsync(message, i => i.Index(index));
                this.logger.LogInformation("Response from ElasticSearchPlugin: {res}", response);
            })]);
        }
        catch(Exception e)
        {
            this.logger.LogError(e, "Error during calling ES");
        }
    }
}
