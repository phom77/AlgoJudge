using AlgoJudge.Application.Interfaces;
using AlgoJudge.Infrastructure.Data;
using AlgoJudge.Infrastructure.Grading;
using AlgoJudge.Infrastructure.Repositories;
using AlgoJudge.Worker;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection must be configured for the worker.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IProblemRepository, ProblemRepository>();
builder.Services.AddScoped<ISubmissionRepository, SubmissionRepository>();
builder.Services.AddScoped<IJudgeTestCaseRepository, JudgeTestCaseRepository>();
builder.Services.AddScoped<IGraderService, GraderService>();
builder.Services.AddScoped<IDockerSandbox, DockerSandboxService>();
builder.Services.AddHostedService<GraderWorker>();

var host = builder.Build();
await host.RunAsync();
