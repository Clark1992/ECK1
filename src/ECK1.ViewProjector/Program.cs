using Confluent.Kafka;
using Confluent.SchemaRegistry;
using ECK1.CommonUtils.Doppler.ConfigurationExtensions;
using ECK1.Contracts.Kafka.BusinessEvents.Sample;
using ECK1.Kafka;
using ECK1.Kafka.Extensions;
using ECK1.Orleans.Extensions;
using ECK1.ViewProjector;
using ECK1.ViewProjector.Data;
using ECK1.ViewProjector.Kafka.Orleans;
using ECK1.ViewProjector.Views;
using ECK1.ViewProjector.Kafka;
using ECK1.ViewProjector.Startup;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;


var builder = WebApplication.CreateBuilder(args);

#if DEBUG
builder.Configuration.AddUserSecrets<Program>();
#endif

builder.Configuration.AddDopplerSecrets();

var configuration = builder.Configuration;
var environment = builder.Environment;

builder.Host.SetupOrleansHosting();

builder.Services.AddControllers();

builder.Services.AddAutoMapper(typeof(Program).Assembly);

var conventionPack = new ConventionPack {
    new CamelCaseElementNameConvention(), 
};

ConventionRegistry.Register("CamelCase", conventionPack, t => true);

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

builder.Services.AddLogging();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

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
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ViewProjector API V1");
    c.RoutePrefix = string.Empty;
});

app.MapControllers();

GenericHandlerBootstrapper.Initialize(typeof(Program).Assembly);

app.Run();
