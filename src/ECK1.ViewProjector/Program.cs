using ECK1.CommonUtils.AspNet;
using ECK1.CommonUtils.Secrets.Doppler;
using ECK1.CommonUtils.Secrets.K8s;
using ECK1.IntegrationContracts.Kafka.IntegrationRecords.Sample;
using ECK1.Orleans.Extensions;
using ECK1.ViewProjector.Data;
using ECK1.ViewProjector.Handlers.Services;
using ECK1.ViewProjector.Kafka;
using ECK1.ViewProjector.Startup;
using ECK1.ViewProjector.Views;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;


var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddK8sSecrets();

#if DEBUG
builder.Configuration.AddUserSecrets<Program>();
#endif

builder.Configuration.AddDopplerSecrets();

var configuration = builder.Configuration;
var environment = builder.Environment;

builder.Host.SetupOrleansHosting();

builder.Services.AddControllers(options =>
    options.Conventions.Add(new RouteTokenTransformerConvention(new SlugifyParameterTransformer())));

builder.Services.AddAutoMapper(typeof(Program).Assembly);

var conventionPack = new ConventionPack {
    new CamelCaseElementNameConvention(), 
};

ConventionRegistry.Register("CamelCase", conventionPack, t => true);

#pragma warning disable CS0618 // Type or member is obsolete
BsonDefaults.GuidRepresentationMode = GuidRepresentationMode.V3;
#pragma warning restore CS0618 // Type or member is obsolete
BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

builder.Services.AddSingleton(sp =>
{
    var mongoConnectionString = configuration["MongoDb:ConnectionString"] ?? throw new Exception("Missing MongoDb:ConnectionString");
    var mongoDatabaseName = configuration["MongoDb:DatabaseName"] ?? throw new Exception("Missing MongoDb:DatabaseName");
    return new MongoDbContext(mongoConnectionString, mongoDatabaseName);
});

builder.Services.SetupKafka(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

builder.Services.AddLogging()
    .AddEndpointsApiExplorer()
    .AddSwaggerGen()
    .AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

builder.Services.AddSingleton<IFullRecordBuilder<SampleView, SampleThinEvent, SampleFullRecord>, SampleFullRecordBuilder>();

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

// Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "View Projector API V1");
    c.RoutePrefix = string.Empty;
});

app.MapControllers();

GenericHandlerBootstrapper.Initialize(typeof(Program).Assembly);

app.Run();
