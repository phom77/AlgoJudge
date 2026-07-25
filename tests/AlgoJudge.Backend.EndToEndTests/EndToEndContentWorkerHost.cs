using AlgoJudge.Application.ContentGeneration;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.ContentWorker;
using AlgoJudge.Infrastructure.ContentGeneration;
using AlgoJudge.Infrastructure.Data;
using AlgoJudge.Infrastructure.Grading;
using AlgoJudge.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AlgoJudge.Backend.EndToEndTests;

internal sealed class EndToEndContentWorkerHost : IAsyncDisposable
{
    private readonly ServiceProvider _services;
    private readonly IReadOnlyCollection<IHostedService> _hostedServices;

    private EndToEndContentWorkerHost(
        ServiceProvider services,
        IReadOnlyCollection<IHostedService> hostedServices)
    {
        _services = services;
        _hostedServices = hostedServices;
    }

    public static async Task<EndToEndContentWorkerHost> StartAsync(
        string connectionString,
        string workerId,
        CapturingLoggerProvider loggerProvider,
        ISourceGenerationSandbox sourceSandbox,
        IFunctionReferenceSolutionRunner referenceRunner,
        IWrongSolutionRunner wrongSolutionRunner)
    {
        var dockerImage = Environment.GetEnvironmentVariable(
            BackendEndToEndFactAttribute.DockerImageEnvironmentVariable)
            ?? throw new InvalidOperationException("Docker judge image is not configured.");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sandbox:DockerImage"] = dockerImage,
                ["ContentGeneration:MaximumCaseCount"] = "1000"
            })
            .Build();
        var queueOptions = new ContentQueueOptions
        {
            WorkerId = workerId,
            PollIntervalSeconds = 1,
            LeaseDurationSeconds = 30,
            HeartbeatIntervalSeconds = 2,
            MaxAttempts = 1
        };
        queueOptions.Validate();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(logging => logging.AddProvider(loggerProvider));
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IContentGenerationJobRepository, ContentGenerationJobRepository>();
        services.AddScoped<IContentGenerationEngine, SandboxedContentGenerationEngine>();
        services.AddSingleton<ISourceGenerationSandbox>(sourceSandbox);
        services.AddSingleton<IFunctionReferenceSolutionRunner>(referenceRunner);
        services.AddSingleton<IWrongSolutionRunner>(wrongSolutionRunner);
        services.AddSingleton<IOutputChecker, OutputChecker>();
        services.AddSingleton(queueOptions);
        services.AddSingleton(ContentWorkerIdentity.Create(workerId));
        services.AddHostedService<ContentGenerationWorker>();

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

            return new EndToEndContentWorkerHost(provider, hostedServices);
        }
        catch
        {
            await provider.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        foreach (var hostedService in _hostedServices.Reverse())
            await hostedService.StopAsync(timeout.Token);
        await _services.DisposeAsync();
    }
}
