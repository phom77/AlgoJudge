using AlgoJudge.Application.FunctionExecution;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Infrastructure.Data;
using AlgoJudge.Infrastructure.Grading;
using AlgoJudge.Infrastructure.Repositories;
using AlgoJudge.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlgoJudge.Backend.EndToEndTests;

internal sealed class EndToEndWorkerHost : IAsyncDisposable
{
    private readonly ServiceProvider _services;
    private readonly IReadOnlyCollection<IHostedService> _hostedServices;

    private EndToEndWorkerHost(
        ServiceProvider services,
        IReadOnlyCollection<IHostedService> hostedServices)
    {
        _services = services;
        _hostedServices = hostedServices;
    }

    public static async Task<EndToEndWorkerHost> StartAsync(
        string connectionString,
        string workerId,
        CapturingLoggerProvider loggerProvider,
        IDockerSandbox? sandbox = null)
    {
        var configuration = CreateSandboxConfiguration();
        var queueOptions = new SubmissionQueueOptions
        {
            WorkerId = workerId,
            PollIntervalSeconds = 1,
            LeaseDurationSeconds = 10,
            HeartbeatIntervalSeconds = 2,
            MaxAttempts = 3
        };
        queueOptions.Validate();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(logging => logging.AddProvider(loggerProvider));
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IProblemRepository, ProblemRepository>();
        services.AddScoped<ISubmissionRepository, SubmissionRepository>();
        services.AddScoped<ITestSuiteProvider, PostgreSqlSystemTestSuiteProvider>();
        services.AddScoped<IGraderService, GraderService>();
        services.AddScoped<IRunRepository, RunRepository>();
        services.AddScoped<IRunGraderService, RunGraderService>();
        services.AddSingleton<IFunctionHarnessBuilder, Cpp17FunctionHarnessBuilder>();
        if (sandbox is null)
            services.AddScoped<IDockerSandbox, DockerSandboxService>();
        else
            services.AddSingleton(sandbox);
        services.AddSingleton(queueOptions);
        services.AddSingleton(WorkerIdentity.Create(workerId));
        services.AddSingleton<WorkerHealthState>();
        services.AddHostedService<GraderWorker>();

        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        var hostedServices = provider.GetServices<IHostedService>().ToArray();
        try
        {
            foreach (var hostedService in hostedServices)
                await hostedService.StartAsync(CancellationToken.None);

            return new EndToEndWorkerHost(provider, hostedServices);
        }
        catch
        {
            await provider.DisposeAsync();
            throw;
        }
    }

    public static IDockerSandbox CreateDockerSandbox()
    {
        return new DockerSandboxService(
            CreateSandboxConfiguration(),
            NullLogger<DockerSandboxService>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        foreach (var hostedService in _hostedServices.Reverse())
            await hostedService.StopAsync(timeout.Token);
        await _services.DisposeAsync();
    }

    private static IConfiguration CreateSandboxConfiguration()
    {
        var image = Environment.GetEnvironmentVariable(
            BackendEndToEndFactAttribute.DockerImageEnvironmentVariable)
            ?? throw new InvalidOperationException(
                "Docker judge image is not configured.");
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sandbox:DockerImage"] = image,
                ["Sandbox:CompileTimeoutSeconds"] = "30",
                ["Sandbox:DockerStartupAllowanceSeconds"] = "15",
                ["Sandbox:StdoutLimitBytes"] = (64 * 1024).ToString(),
                ["Sandbox:StderrLimitBytes"] = (64 * 1024).ToString(),
                ["Sandbox:PidsLimit"] = "32"
            })
            .Build();
    }
}
