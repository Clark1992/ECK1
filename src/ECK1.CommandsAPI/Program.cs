using ECK1.CommandsAPI;
using ECK1.CommandsAPI.Data;
using ECK1.CommandsAPI.Kafka;
using ECK1.CommandsAPI.Startup;
using ECK1.CommonUtils.AspNet;
using ECK1.CommonUtils.Secrets.Doppler;
using ECK1.CommonUtils.Secrets.K8s;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddK8sSecrets();

#if DEBUG
builder.Configuration.AddUserSecrets<Program>();
#endif

builder.Configuration.AddDopplerSecrets();

var configuration = builder.Configuration;
var environment = builder.Environment;

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
builder.Services.AddSwaggerGen();
builder.Services.AddAutoMapper(typeof(Program).Assembly);

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
builder.Services.Configure<EventsStoreConfig>(
    builder.Configuration.GetSection(nameof(EventsStoreConfig))
);
builder.Services.AddScoped<SampleRepo>();

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
