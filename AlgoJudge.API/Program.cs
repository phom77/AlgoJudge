using AlgoJudge.Application.Interfaces;
using AlgoJudge.Application.Mappings;
using AlgoJudge.Application.Services;
using AlgoJudge.Infrastructure.Data;
using AlgoJudge.Infrastructure.Grading;
using AlgoJudge.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using AlgoJudge.Infrastructure.Grading;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

//AutoMapper
builder.Services.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());

// Dependency Injection 
builder.Services.AddScoped<IProblemRepository, ProblemRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IProblemService, ProblemService>();
builder.Services.AddScoped<ISubmissionRepository, SubmissionRepository>();
builder.Services.AddScoped<ISubmissionService, SubmissionService>();
builder.Services.AddScoped<ITestCaseRepository, TestCaseRepository>();
builder.Services.AddScoped<IGraderService, GraderService>();
builder.Services.AddHostedService<GraderWorker>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
