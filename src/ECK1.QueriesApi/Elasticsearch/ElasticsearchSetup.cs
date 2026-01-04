using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Serialization;
using Elastic.Transport;
using Microsoft.Extensions.Options;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ECK1.QueriesAPI.Elasticsearch;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection SetupElasticsearch(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<ElasticSearchConfig>(config.GetSection(nameof(ElasticSearchConfig)));

        services.AddSingleton<ElasticsearchClient>(sp =>
        {
            var config = sp.GetRequiredService<IOptions<ElasticSearchConfig>>().Value;

            if (string.IsNullOrWhiteSpace(config.ClusterUrl))
                throw new Exception("ElasticSearchConfig:ClusterUrl is missing");

            if (string.IsNullOrWhiteSpace(config.ApiKey))
                throw new Exception("ElasticSearchConfig:ApiKey is missing");

            var settings = new ElasticsearchClientSettings(
                new SingleNodePool(new Uri(config.ClusterUrl)),
                sourceSerializer: (defaultSerializer, settings) => 
                    new DefaultSourceSerializer(settings, ConfigureOptions))
                .Authentication(new ApiKey(config.ApiKey));

            static void ConfigureOptions(JsonSerializerOptions o) =>
                   o.Converters.Add(new JsonStringEnumConverter());

#if DEBUG
            settings = settings
                .DisableDirectStreaming()
                .PrettyJson()
                .OnRequestCompleted(details =>
                {
                    if (details.RequestBodyInBytes != null)
                    {
                        var json = Encoding.UTF8.GetString(details.RequestBodyInBytes);
                    }
                });
#endif

            // If running against TLS with a private CA in k8s, mount it at /etc/elasticsearch/certs/ca.crt.
            var caPath = "/etc/elasticsearch/certs/ca.crt";
            if (File.Exists(caPath))
            {
                var caCert = new X509Certificate2(caPath);

                settings = settings.ServerCertificateValidationCallback((_, cert, _, _) =>
                {
                    if (cert is not X509Certificate2 cert2)
                        return false;

                    var chain = new X509Chain();
                    chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                    chain.ChainPolicy.ExtraStore.Add(caCert);
                    chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
                    chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                    chain.ChainPolicy.CustomTrustStore.Add(caCert);
                    return chain.Build(cert2);
                });
            }
            else if (config.ClusterUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception($"ElasticSearch CA certificate not found at {caPath}");
            }

            return new ElasticsearchClient(settings);
        });

        return services;
    }
}