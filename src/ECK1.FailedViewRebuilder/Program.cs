using ECK1.CommonUtils.AspNet;
using ECK1.CommonUtils.JobQueue;
using ECK1.CommonUtils.Secrets.Doppler;
using ECK1.CommonUtils.Secrets.K8s;
using ECK1.FailedViewRebuilder.Data;
using ECK1.FailedViewRebuilder.Kafka;
using ECK1.FailedViewRebuilder.Services;
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

builder.Services.AddDbContext<FailuresDbContext>(options =>
    options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAutoMapper(typeof(Program).Assembly);

builder.Services.SetupKafka(builder.Configuration);

builder.Services.AddScoped(typeof(IRebuildRequestService<,>), typeof(RebuildRequestService<,>));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

builder.Services.AddLogging();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAutoMapper(typeof(Program).Assembly);

builder.Services.AddQueueProcessing(c => c.AddRunner(typeof(IJobRunner<,>), typeof(JobRunner<,>)));

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
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Failed View Rebuilder Service API V1");
    c.RoutePrefix = string.Empty;
});

app.MapControllers();

app.Run();
