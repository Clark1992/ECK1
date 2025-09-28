using Confluent.Kafka;
using DbUp;
using ECK1.CommandsAPI;
using ECK1.CommandsAPI.Data;
using ECK1.CommandsAPI.Startup;
using ECK1.Contracts.Kafka.BusinessEvents.Sample;
using ECK1.Kafka.Extensions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

#if DEBUG
builder.Configuration.AddUserSecrets<Program>();
#endif

builder.Configuration.AddDopplerSecrets();

var configuration = builder.Configuration;
var environment = builder.Environment;

builder.Services.AddControllers();

builder.Services.AddDbContext<CommandsDbContext>(options =>
    options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

builder.Services.AddLogging();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAutoMapper(typeof(Program).Assembly);

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
builder.Services.Configure<EventsStoreConfig>(
    builder.Configuration.GetSection(nameof(EventsStoreConfig))
);
builder.Services.AddScoped<SampleRepo>();

#region Kafka

var kafkaSettings = builder.Configuration
    .GetSection(KafkaSettings.Section)
    .Get<KafkaSettings>();

builder.Services
    .AddKafkaRootProducer(kafkaSettings.BootstrapServers,
    c =>
    {
        c.Acks = Acks.Leader;
        c.WithAuth(kafkaSettings.User, kafkaSettings.Secret);
    })
    .WithSchemaRegistry(kafkaSettings.SchemaRegistryUrl,
        c => c.WithAuth(kafkaSettings.User, kafkaSettings.Secret));

builder.Services.ConfigTopicProducer<ISampleEvent>(
    kafkaSettings.SampleBusinessEventsTopic, 
    Confluent.SchemaRegistry.SubjectNameStrategy.Topic,
    SerializerType.JSON);

#endregion

var app = builder.Build();

if (configuration.GetValue<bool>("DbUp:RunMigrations"))
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Running database migrations with DbUp...");

    var connectionString = configuration.GetConnectionString("DefaultConnection");

    var upgrader = DeployChanges.To
        .SqlDatabase(connectionString)
        .WithScriptsFromFileSystem(Path.Combine(AppContext.BaseDirectory, "Migrations"))
        .LogToConsole()
        .Build();

    var result = upgrader.PerformUpgrade();

    if (!result.Successful)
    {
        logger.LogError(result.Error, "❌ Migration failed");
        return;
    }

    logger.LogDebug("Migrations applied successfully.");
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
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Commands API V1");
    c.RoutePrefix = string.Empty;
});

app.MapControllers();

AggregateHandlerBootstrapper.Initialize(typeof(Program).Assembly);
IntegrationHandlerBootstrapper.Initialize(typeof(Program).Assembly);

app.Run();
