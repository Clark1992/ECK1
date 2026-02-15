using ECK1.CommonUtils.AspNet;
using ECK1.Integration.Cache.LongTerm;
using ECK1.Integration.Cache.LongTerm.Kafka;
using ECK1.Integration.Cache.LongTerm.Services;
using ECK1.Integration.Common;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using ProtoBuf.Grpc.Server;
using ECK1.Integration.EntityStore.Generated;
using ECK1.CommonUtils.OpenTelemetry;
using ECK1.Kafka.OpenTelemetry;

var builder = WebApplication.CreateBuilder(args);

builder.AddConfigSources();

builder.AddOpenTelemetry(tracingExtraConfig: tracing => tracing
    .AddKafkaInstrumentation()
    .AddNatsInstrumentation());

builder.Services.Configure<CacheConfig>(builder.Configuration.GetSection(CacheConfig.Section));
builder.Services.Configure<NatsSettings>(builder.Configuration.GetSection(NatsSettings.Section));

builder.Services.AddSingleton<INatsTelemetry, NatsTelemetry>();
builder.Services.AddSingleton<IEntityStore, NatsEntityStore>();
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
builder.Services.AddAutoMapper(typeof(Program).Assembly);
builder.Services.AddGrpc();

var integrationConfig = ConfigHelpers.LoadConfig(builder.Configuration);

builder.Services.SetupKafka(integrationConfig, builder.Configuration);

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
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Integration Cache LongTerm API V1");
    c.RoutePrefix = string.Empty;
});

app.MapControllers();

app.MapGrpcService<EntityStoreGrpcService>();

app.Run();
