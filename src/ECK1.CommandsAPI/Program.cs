using ECK1.AsyncApi.Extensions;
using ECK1.CommandsAPI;
using ECK1.CommandsAPI.Commands;
using ECK1.CommandsAPI.Data;
using ECK1.CommandsAPI.Data.Models;
using ECK1.CommandsAPI.Domain.Sample2s;
using ECK1.CommandsAPI.Domain.Samples;
using ECK1.CommandsAPI.Kafka;
using ECK1.CommandsAPI.Kafka.Orleans;
using ECK1.CommandsAPI.Startup;
using ECK1.CommonUtils.ActorContext;
using ECK1.CommonUtils.AspNet;
using ECK1.CommonUtils.Chaos;
using ECK1.Integration.Config;
using ECK1.CommonUtils.OpenTelemetry;
using ECK1.CommonUtils.Secrets.Doppler;
using ECK1.CommonUtils.Secrets.K8s;
using ECK1.IntegrationContracts.Kafka.IntegrationRecords.Sample;
using ECK1.IntegrationContracts.Kafka.IntegrationRecords.Sample2;
using ECK1.Kafka.OpenTelemetry;
using ECK1.Orleans.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Trace;
using ECK1.CommonUtils.Swagger;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddK8sSecrets();

#if DEBUG
builder.Configuration.AddUserSecrets<Program>();
#endif

builder.Configuration.AddDopplerSecrets();
builder.Configuration.AddIntegrationManifest();

var configuration = builder.Configuration;
var environment = builder.Environment;

builder.Host.SetupOrleansHosting();

builder.AddOpenTelemetry(tracingExtraConfig: tracing => tracing
    .AddKafkaInstrumentation()
    .AddSqlClientInstrumentation(options =>
    {
        options.RecordException = true;
    })
    .AddSource("Microsoft.Orleans.Runtime")
    .AddSource("Microsoft.Orleans.Application"));

builder.Services.AddControllers(options =>
    options.Conventions.Add(new RouteTokenTransformerConvention(new SlugifyParameterTransformer())));

builder.Services.AddDbContext<CommandsDbContext>(options =>
    options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

builder.Services.AddLogging();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.OperationFilter<RequirePermissionOperationFilter>();
});
builder.Services.AddAutoMapper(typeof(Program).Assembly);

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
builder.Services.Configure<EventsStoreConfig>(
    builder.Configuration.GetSection(nameof(EventsStoreConfig))
);
builder.Services.AddScoped<IRootRepository<Sample>, RootRepository<Sample, SampleEventEntity, SampleSnapshotEntity>>();
builder.Services.AddScoped<IRootRepository<Sample2>, RootRepository<Sample2, Sample2EventEntity, Sample2SnapshotEntity>>();

builder.Services.SetupOrleansDefaults();

builder.Services.AddGrain<ISampleCommand>()
    .AsStateful<Sample, ICommandResult>()
    .HandledBy<CommandGrainHandler<ISampleCommand, Sample>>();

builder.Services.AddGrain<ISample2Command>()
    .AsStateful<Sample2, ICommandResult>()
    .HandledBy<CommandGrainHandler<ISample2Command, Sample2>>();

builder.Services.AddGrain<RebuildSampleViewCommand>()
    .AsStateful<Sample, ICommandResult>()
    .HandledBy<CommandGrainHandler<RebuildSampleViewCommand, Sample>>();

builder.Services.AddGrain<RebuildSample2ViewCommand>()
    .AsStateful<Sample2, ICommandResult>()
    .HandledBy<CommandGrainHandler<RebuildSample2ViewCommand, Sample2>>();

builder.Services.SetupKafka(builder.Configuration);

builder.Services.AddChaosEngine(builder.Configuration);

builder.Services.AddScoped<INotificationHandler<AggregateSavedNotification<Sample>>, IntegrationSender<Sample, SampleFullRecord>>();
builder.Services.AddScoped<INotificationHandler<AggregateSavedNotification<Sample2>>, IntegrationSender<Sample2, Sample2FullRecord>>();

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
app.UseActorLogging();

app.UseAuthorization();

// Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("swagger/v1/swagger.json", "Commands API V1");
    c.RoutePrefix = string.Empty;
});

app.MapControllers();
app.MapChaosEndpoints();
app.MapAsyncApiDocument(
    Environment.GetEnvironmentVariable("SERVICE_NAME") ?? 
        throw new Exception("SERVICE_NAME env var not specified."), 
    typeof(Program).Assembly);

AggregateHandlerBootstrapper.Initialize(typeof(Program).Assembly);

app.Run();
