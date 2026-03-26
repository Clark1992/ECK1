using ECK1.CommonUtils.AspNet;
using ECK1.CommonUtils.OpenTelemetry;
using ECK1.CommonUtils.Secrets.Doppler;
using ECK1.CommonUtils.Secrets.K8s;
using ECK1.Gateway.Startup;
using ECK1.Gateway.Workers;
using ECK1.Kafka.OpenTelemetry;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddK8sSecrets();

#if DEBUG
builder.Configuration.AddUserSecrets<Program>();
#endif

builder.Configuration.AddDopplerSecrets();

builder.AddOpenTelemetry(tracingExtraConfig: tracing => tracing
    .AddKafkaInstrumentation());

builder.Services
    .AddGatewayOptions(builder.Configuration)
    .AddServiceDiscovery(builder.Configuration)
    .AddGatewayProxy()
    .AddGatewaySwagger()
    .AddCommandPipeline(builder.Configuration)
    .AddHostedService<GatewayRefreshWorker>();

var app = builder.Build();

app.UseTraceResponseEnricher();

app.MapSwaggerEndpoints();
app.MapGatewayEndpoints();

app.Run();
