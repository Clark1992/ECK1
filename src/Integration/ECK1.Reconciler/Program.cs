using ECK1.CommonUtils.AspNet;
using ECK1.CommonUtils.Chaos;
using ECK1.CommonUtils.OpenTelemetry;
using ECK1.CommonUtils.Secrets.Doppler;
using ECK1.CommonUtils.Secrets.K8s;
using ECK1.Integration.Config;
using ECK1.Kafka.OpenTelemetry;
using ECK1.Reconciler;
using ECK1.Reconciler.Data;
using ECK1.Reconciler.Kafka;
using ECK1.Reconciler.Services;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Trace;

using static ECK1.Integration.Config.ConfigHelpers;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddK8sSecrets();

#if DEBUG
builder.Configuration.AddUserSecrets<Program>();
#endif

builder.Configuration.AddDopplerSecrets();
builder.Configuration.AddIntegrationManifest();

var configuration = builder.Configuration;
IntegrationConfig integrationConfig = LoadConfig(configuration);
builder.Services.AddSingleton(integrationConfig);

builder.Services.Configure<ReconcilerSettings>(
    configuration.GetSection(ReconcilerSettings.Section));

builder.AddOpenTelemetry(tracingExtraConfig: tracing => tracing
    .AddKafkaInstrumentation()
    .AddSqlClientInstrumentation(options =>
    {
        options.RecordException = true;
    }));

builder.Services.AddControllers(options =>
    options.Conventions.Add(new RouteTokenTransformerConvention(new SlugifyParameterTransformer())));

builder.Services.AddDbContext<ReconcilerDbContext>(options =>
    options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ReconcilerRepository>();

builder.Services.AddAutoMapper(typeof(Program).Assembly);

builder.Services.SetupKafka(configuration, integrationConfig);

builder.Services.AddChaosEngine(builder.Configuration);

builder.Services.AddHostedService<ReconciliationCheckService>();
builder.Services.AddHostedService<RebuildDispatchService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

builder.Services.AddLogging();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseRouting();
app.UseCors("AllowAllOrigins");
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ECK1 Reconciler API V1");
    c.RoutePrefix = string.Empty;
});

app.MapControllers();
app.MapChaosEndpoints();

app.Run();
