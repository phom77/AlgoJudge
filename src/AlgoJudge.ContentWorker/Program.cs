using AlgoJudge.Application.ContentGeneration;
using AlgoJudge.Application.FunctionExecution;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.ContentWorker;
using AlgoJudge.Infrastructure.ContentGeneration;
using AlgoJudge.Infrastructure.Data;
using AlgoJudge.Infrastructure.Grading;
using AlgoJudge.Infrastructure.Health;
using AlgoJudge.Infrastructure.Repositories;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);
var queueOptions = builder.Configuration.GetSection("ContentQueue").Get<ContentQueueOptions>() ?? new();
queueOptions.Validate();
var identity = ContentWorkerIdentity.Create(queueOptions.WorkerId);
_ = DockerSandboxOptions.FromConfiguration(builder.Configuration);
_ = DotNetSourceSandboxOptions.FromConfiguration(builder.Configuration);
var connectionString = PostgreSqlHealthCheck.ValidateConnectionString(
    builder.Configuration.GetConnectionString("DefaultConnection"), "content worker");

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<IContentGenerationJobRepository, ContentGenerationJobRepository>();
builder.Services.AddScoped<IContentGenerationEngine, SandboxedContentGenerationEngine>();
builder.Services.AddScoped<ISourceGenerationSandbox, DotNetSourceGenerationSandbox>();
builder.Services.AddScoped<IDockerSandbox, DockerSandboxService>();
builder.Services.AddScoped<IFunctionReferenceSolutionRunner, Cpp17ContentReferenceRunner>();
builder.Services.AddScoped<IWrongSolutionRunner, Cpp17ContentWrongSolutionRunner>();
builder.Services.AddSingleton<IFunctionHarnessBuilder, Cpp17FunctionHarnessBuilder>();
builder.Services.AddSingleton(queueOptions);
builder.Services.AddSingleton(identity);
builder.Services.AddHostedService<ContentGenerationWorker>();
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live", "ready"])
    .AddCheck("postgresql", new PostgreSqlHealthCheck(connectionString), HealthStatus.Unhealthy, tags: ["ready"]);
var app = builder.Build();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = item => item.Tags.Contains("live") });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = item => item.Tags.Contains("ready") });
await app.RunAsync();

public partial class Program;
