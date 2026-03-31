using ECK1.CommonUtils.AspNet;
using ECK1.CommonUtils.Chaos;
using ECK1.CommonUtils.JobQueue;
using ECK1.CommonUtils.OpenTelemetry;
using ECK1.CommonUtils.Secrets.Doppler;
using ECK1.CommonUtils.Secrets.K8s;
using ECK1.FailedViewRebuilder;
using ECK1.FailedViewRebuilder.Data;
using ECK1.FailedViewRebuilder.Kafka;
using ECK1.FailedViewRebuilder.Services;
using ECK1.Integration.Config;
using ECK1.Kafka.OpenTelemetry;
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

builder.AddOpenTelemetry(tracingExtraConfig: tracing => tracing
    .AddKafkaInstrumentation()
    .AddSqlClientInstrumentation(options =>
    {
        options.RecordException = true;
    }));

builder.Services.AddControllers(options => 
    options.Conventions.Add(new RouteTokenTransformerConvention(new SlugifyParameterTransformer())));

builder.Services.AddDbContext<FailuresDbContext>(options =>
    options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAutoMapper(typeof(Program).Assembly);

builder.Services.SetupKafka(builder.Configuration, integrationConfig);

builder.Services.AddChaosEngine(builder.Configuration);

builder.Services.AddScoped<IRebuildRequestService, RebuildRequestService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

builder.Services.AddLogging();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddQueueProcessing(c => c.AddRunner<IJobRunner, JobRunner>());

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

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("AllowAllOrigins");
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("swagger/v1/swagger.json", "Failed View Rebuilder API V1");
    c.RoutePrefix = string.Empty;
});

app.MapControllers();
app.MapChaosEndpoints();

// Register endpoints per entity type from integration manifest
var entityTypes = integrationConfig.Values
    .Where(e => !string.IsNullOrEmpty(e.RebuildRequestTopic))
    .Select(e => e.EntityType)
    .Distinct();

foreach (var entityType in entityTypes)
{
    var slug = entityType.Split('.').Last().ToLower();

    app.MapPost($"/api/jobs/rebuild-failed/{slug}s", async (
        IRebuildRequestService service,
        int? count) =>
    {
        var result = await service.StartJob(entityType, count);
        return Results.Accepted(null, result);
    });

    app.MapDelete($"/api/jobs/rebuild-failed/{slug}s", async (
        IRebuildRequestService service) =>
    {
        var result = await service.StopJob(entityType);
        return Results.Ok(result == 0 ? $"[{entityType}] No job(s) in progress" : $"[{entityType}] stopped {result} job(s).");
    });

    app.MapGet($"/api/jobs/rebuild-failed/{slug}s", async (
        IRebuildRequestService service) =>
    {
        var result = await service.GetStatus(entityType);
        return Results.Ok($"[{entityType}] {result} job(s) in progress.");
    });

    app.MapGet($"/api/failed/{slug}s", async (
        IRebuildRequestService service,
        int? count) =>
    {
        var result = await service.GetFailedViewsOverview(entityType, count);
        return Results.Ok(result);
    });
}

app.Run();
