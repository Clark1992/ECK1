using ECK1.CommonUtils.AspNet;

using ECK1.Integration.Plugin.Abstractions;
using ECK1.Integration.Proxy;
using ECK1.Integration.Proxy.Kafka;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using System.Text.Json;

using static ECK1.Integration.Plugin.Abstractions.ConfigHelpers;

var builder = WebApplication.CreateBuilder(args);

builder.AddConfigSources();

var configuration = builder.Configuration;

var proxyConfig = builder.GetProxyType();

builder.Services.AddLogging();

#pragma warning disable ASP0000 // Do not call 'IServiceCollection.BuildServiceProvider' in 'ConfigureServices'
using var tmp = builder.Services.BuildServiceProvider();
#pragma warning restore ASP0000 // Do not call 'IServiceCollection.BuildServiceProvider' in 'ConfigureServices'
var logger = tmp.GetRequiredService<ILogger<IntergationPluginRegistry>>();

var integrationConfig = builder.GetIntegrationConfig(proxyConfig.Plugin);

var programLogger = tmp.GetRequiredService<ILogger<Program>>();
var normalized = Normalize(integrationConfig);

var json = JsonSerializer.Serialize(normalized, new JsonSerializerOptions { WriteIndented = true });

programLogger.LogInformation("Starting with integrationConfig:\n {config}", json);

builder.Services.AddControllers(options =>
    options.Conventions.Add(new RouteTokenTransformerConvention(new SlugifyParameterTransformer())));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAutoMapper(typeof(Program).Assembly);

builder.Services.SetupKafka(builder.Configuration, integrationConfig, proxyConfig.Plugin);

new IntergationPluginRegistry(logger).LoadPlugin(
    builder.Services, 
    builder.Configuration,
    proxyConfig,
    integrationConfig);

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

// Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", $"Integration Proxy ({proxyConfig.Plugin}) API V1");
    c.RoutePrefix = string.Empty;
});

app.MapControllers();

app.Run();
