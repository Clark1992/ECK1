using System.Text.Json.Serialization;
using ECK1.CommonUtils.AspNet;
using ECK1.TestPlatform.Data;
using ECK1.TestPlatform.Hubs;
using ECK1.TestPlatform.Scenarios;
using ECK1.TestPlatform.Services;
using k8s;
using MediatR;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options =>
    options.Conventions.Add(new RouteTokenTransformerConvention(new SlugifyParameterTransformer())))
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        policy => policy
            .SetIsOriginAllowed(_ => true)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});

builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
}).AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddLogging();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TestPlatform API",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Paste the raw access token. Swagger UI will send it as a Bearer token."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = "Bearer"
            }
        }] = []
    });
});

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

builder.Services.AddSingleton<FakeSampleDataFactory>();
builder.Services.AddSingleton<FakeSample2DataFactory>();
builder.Services.AddSingleton<LoadRunner>();
builder.Services.AddSingleton<InterleavedCreateUpdateRunner>();
builder.Services.AddSingleton<InterleavedTwoPoolCreateUpdateRunner>();

builder.Services.Configure<CommandsApiClientOptions>(
    builder.Configuration.GetSection(CommandsApiClientOptions.SectionName));

builder.Services.Configure<QueriesApiClientOptions>(
    builder.Configuration.GetSection(QueriesApiClientOptions.SectionName));

builder.Services.Configure<GatewayRealtimeClientOptions>(
    builder.Configuration.GetSection(GatewayRealtimeClientOptions.SectionName));

builder.Services.AddSingleton<BearerTokenStore>();
builder.Services.AddSingleton<GatewayRealtimeClient>();
builder.Services.AddTransient<ForwardedTokenHandler>();

builder.Services.Configure<ProxyServiceConfig>(
    builder.Configuration.GetSection(ProxyServiceConfig.SectionName));

// Chaos clients: keyed IChaosClient per target (proxy plugins + reconciler)
builder.Services.AddSingleton<KubernetesPodDiscovery>();
builder.Services.AddHttpClient("chaos");

var proxyPlugins = builder.Configuration
    .GetSection("ProxyServices:Plugins")
    .Get<Dictionary<string, string>>() ?? [];

bool inCluster = KubernetesClientConfiguration.IsInCluster();

foreach (var (name, url) in proxyPlugins)
{
    builder.Services.AddKeyedSingleton<IChaosClient>(name, (sp, _) =>
        inCluster
            ? new KubernetesChaosClient(
                sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetRequiredService<KubernetesPodDiscovery>(),
                "chaos", url,
                sp.GetRequiredService<ILogger<KubernetesChaosClient>>())
            : new DirectChaosClient(
                sp.GetRequiredService<IHttpClientFactory>(),
                "chaos", url,
                sp.GetRequiredService<ILogger<DirectChaosClient>>()));
}

var reconcilerUrl = builder.Configuration["ReconcilerApi:BaseUrl"] ?? "";
builder.Services.AddKeyedSingleton<IChaosClient>("reconciler", (sp, _) =>
    inCluster
        ? new KubernetesChaosClient(
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<KubernetesPodDiscovery>(),
            "chaos", reconcilerUrl,
            sp.GetRequiredService<ILogger<KubernetesChaosClient>>())
        : new DirectChaosClient(
            sp.GetRequiredService<IHttpClientFactory>(),
            "chaos", reconcilerUrl,
            sp.GetRequiredService<ILogger<DirectChaosClient>>()));

builder.Services.AddSingleton<ChaosManager>();

builder.Services.AddHttpClient<CommandsApiClient>()
    .AddHttpMessageHandler<ForwardedTokenHandler>();
builder.Services.AddHttpClient<QueriesApiClient>();

builder.Services.AddSingleton<StorageVersionChecker>();

// Scenarios
builder.Services.AddSingleton<IScenario, MissedUpdatesAndSelfHealScenario>();
builder.Services.AddSingleton<ScenarioRegistry>();

// Run history DB (SQLite) — use /data mount if available (PVC), otherwise ContentRootPath
var dataDir = Directory.Exists("/data") ? "/data" : builder.Environment.ContentRootPath;
var dbPath = Path.Combine(dataDir, "testplatform-runs.db");
builder.Services.AddDbContext<RunHistoryDb>(opts => opts.UseSqlite($"Data Source={dbPath}"));
builder.Services.AddSingleton<RunStore>();

var app = builder.Build();

// Auto-migrate DB
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RunHistoryDb>();
    db.Database.EnsureCreated();
}

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

// Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "TestPlatform API V1");
    c.RoutePrefix = string.Empty;
});

app.MapControllers();
app.MapHub<ScenarioHub>("/hubs/scenarios");

app.Run();
