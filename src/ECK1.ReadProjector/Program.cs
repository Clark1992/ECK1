using Confluent.SchemaRegistry;
using ECK1.CommonUtils.Doppler.ConfigurationExtensions;
using ECK1.Kafka;
using ECK1.Kafka.Extensions;
using ECK1.Orleans.Extensions;
using ECK1.ReadProjector;
using ECK1.ReadProjector.Data;
using ECK1.ReadProjector.OrleansKafka;
using ECK1.ReadProjector.Startup;
using ECK1.ReadProjector.Views;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

using Contract = ECK1.Contracts.Kafka.BusinessEvents;
using ViewEvent = ECK1.ReadProjector.Events;

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

#region Kafka + Orleans


builder.Services.AddSingleton(
    typeof(IKafkaMessageHandler<Contract.Sample.ISampleEvent>), 
    typeof(OrleansKafkaAdapter<Contract.Sample.ISampleEvent, ViewEvent.ISampleEvent, SampleEventKafkaMetadata>));

builder.Services.AddKafkaGrainRouter<
    ViewEvent.ISampleEvent,
    SampleEventKafkaMetadata,
    SampleView,
    KafkaMessageHandler<ViewEvent.ISampleEvent, SampleView>>(
    ev => ev.SampleId.ToString())
    .AddDupChecker<SampleEventKafkaMetadata>()
    .AddMetadataUpdater<SampleEventKafkaMetadata>()
    .AddFaultedStateReset<SampleEventKafkaMetadata>();

var kafkaSettings = builder.Configuration
    .GetSection(KafkaSettings.Section)
    .Get<KafkaSettings>();

builder.Services
    .WithSchemaRegistry(kafkaSettings.SchemaRegistryUrl,
        c => c.WithAuth(kafkaSettings.User, kafkaSettings.Secret));

builder.Services.ConfigTopicConsumer<Contract.Sample.ISampleEvent>(
    kafkaSettings.BootstrapServers,
    kafkaSettings.SampleBusinessEventsTopic,
    kafkaSettings.GroupId,
    SubjectNameStrategy.Topic,
    SerializerType.JSON,
    c =>
    {
        c.WithAuth(kafkaSettings.User, kafkaSettings.Secret);
    });

builder.Services.AddHostedService<KafkaTopicConsumerService>();

#endregion

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
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Queries API V1");
    c.RoutePrefix = string.Empty;
});

app.MapControllers();

GenericHandlerBootstrapper.Initialize(typeof(Program).Assembly);

app.Run();
