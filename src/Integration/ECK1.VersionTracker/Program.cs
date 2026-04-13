using ECK1.CommonUtils.AspNet;
using ECK1.CommonUtils.OpenTelemetry;
using ECK1.CommonUtils.Secrets.K8s;
using ECK1.Integration.Config;
using ECK1.VersionTracker;
using ECK1.VersionTracker.Kafka;
using ECK1.VersionTracker.Services;
using ECK1.VersionTracker.Storage;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using ProtoBuf.Grpc.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddK8sSecrets();
builder.Configuration.AddIntegrationManifest();

var configuration = builder.Configuration;

builder.AddOpenTelemetry();

var mongoSettings = configuration.GetSection(MongoDbSettings.Section).Get<MongoDbSettings>()
    ?? throw new InvalidOperationException("MongoDb settings are missing.");
builder.Services.AddSingleton(mongoSettings);
builder.Services.AddSingleton<VersionStore>();
builder.Services.AddControllers(options =>
    options.Conventions.Add(new RouteTokenTransformerConvention(new SlugifyParameterTransformer())));

builder.Services.AddCodeFirstGrpc();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

builder.Services.AddLogging();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.SetupKafka(builder.Configuration);

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
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Version Tracker API V1");
    c.RoutePrefix = string.Empty;
});

app.MapControllers();
app.MapGrpcService<VersionTrackerGrpcService>();

app.Run();
