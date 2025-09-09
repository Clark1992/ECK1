using ECK1.QueriesApi.Data;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

var builder = WebApplication.CreateBuilder(args);

var configuration = builder.Configuration;
var environment = builder.Environment;

builder.Services.AddControllers();

var conventionPack = new ConventionPack { new CamelCaseElementNameConvention(), //new IgnoreExtraElementsConvention(true)
                                                                                };
ConventionRegistry.Register("CamelCase", conventionPack, t => true);

builder.Services.AddSingleton(sp =>
{
    var mongoConnectionString = configuration["MongoDb:ConnectionString"] ?? throw new Exception("Missing MongoDb:ConnectionString");
    var mongoDatabaseName = configuration["MongoDb:DatabaseName"] ?? throw new Exception("Missing MongoDb:DatabaseName");
    return new MongoDbContext(mongoConnectionString, mongoDatabaseName);
});

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

app.Run();
