using AlgoJudge.Application.Interfaces;
using AlgoJudge.Infrastructure.Data;
using AlgoJudge.Infrastructure.Grading;
using AlgoJudge.Infrastructure.Health;
using AlgoJudge.Infrastructure.Repositories;
using AlgoJudge.Worker;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    options.UseUtcTimestamp = true;
});

var queueOptions = builder.Configuration
    .GetSection("Queue")
    .Get<SubmissionQueueOptions>() ?? new SubmissionQueueOptions();
queueOptions.Validate();
var workerIdentity = WorkerIdentity.Create(queueOptions.WorkerId);

_ = DockerSandboxOptions.FromConfiguration(builder.Configuration);

var connectionString = PostgreSqlHealthCheck.ValidateConnectionString(
    builder.Configuration.GetConnectionString("DefaultConnection"),
    "worker");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IProblemRepository, ProblemRepository>();
builder.Services.AddScoped<ISubmissionRepository, SubmissionRepository>();
builder.Services.AddScoped<IJudgeTestCaseRepository, JudgeTestCaseRepository>();
builder.Services.AddScoped<IGraderService, GraderService>();
builder.Services.AddScoped<IDockerSandbox, DockerSandboxService>();
builder.Services.AddSingleton(queueOptions);
builder.Services.AddSingleton(workerIdentity);
builder.Services.AddSingleton<WorkerHealthState>();
builder.Services.AddHostedService<GraderWorker>();

builder.Services.AddHealthChecks()
    .AddCheck<WorkerProcessHealthCheck>(
        "worker",
        tags: ["live", "ready"])
    .AddCheck(
        "postgresql",
        new PostgreSqlHealthCheck(connectionString),
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"]);

var app = builder.Build();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live"),
    ResponseWriter = WorkerHealthResponseWriter.WriteAsync
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = WorkerHealthResponseWriter.WriteAsync
});

await app.RunAsync();
