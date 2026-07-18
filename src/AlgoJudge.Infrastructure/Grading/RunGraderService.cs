using AlgoJudge.Application.FunctionExecution;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Application.Models.RunQueue;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace AlgoJudge.Infrastructure.Grading;

public sealed class RunGraderService : IRunGraderService
{
    private readonly IRunRepository _runs;
    private readonly IProblemRepository _problems;
    private readonly IDockerSandbox _sandbox;
    private readonly IFunctionHarnessBuilder _functionHarnessBuilder;
    private readonly ILogger<RunGraderService> _logger;

    public RunGraderService(IRunRepository runs, IProblemRepository problems, IDockerSandbox sandbox,
        IFunctionHarnessBuilder functionHarnessBuilder, ILogger<RunGraderService> logger)
    {
        _runs = runs; _problems = problems; _sandbox = sandbox;
        _functionHarnessBuilder = functionHarnessBuilder; _logger = logger;
    }

    public async Task GradeAsync(RunClaim claim, CancellationToken cancellationToken = default)
    {
        var run = await _runs.GetClaimedAsync(claim, cancellationToken);
        if (run is null) throw new RunClaimLostException(claim.RunId);
        var problem = await _problems.GetByIdAsync(run.ProblemId);
        if (problem is null)
        {
            _logger.LogError("Problem {ProblemId} is missing for claimed run {RunId}.", run.ProblemId, run.Id);
            await FinalizeAsync(claim, RunStatus.RuntimeError, null, "The problem configuration is unavailable.", 0, 0, cancellationToken);
            return;
        }

        var workDirectory = Path.Combine(
            Path.GetTempPath(),
            "algojudge",
            $"run-{run.Id}-{claim.ClaimToken:N}");
        try
        {
            Directory.CreateDirectory(workDirectory);
            var source = BuildSource(problem, run.SourceCode);
            var compilation = await _sandbox.CompileAsync(source, workDirectory, cancellationToken);
            if (!compilation.Success)
            {
                await FinalizeAsync(claim, RunStatus.CompileError, null, compilation.ErrorOutput, 0, 0, cancellationToken);
                return;
            }

            var result = await _sandbox.RunAsync(workDirectory, run.Input, problem.TimeLimitMs, problem.MemoryLimitKb, cancellationToken);
            if (result.Status == SandboxRunStatus.SystemError)
                throw new InvalidOperationException($"Sandbox system error while executing run {run.Id}.");
            var status = result.Status switch
            {
                SandboxRunStatus.Success => RunStatus.Completed,
                SandboxRunStatus.TimeLimitExceeded => RunStatus.TimeLimitExceeded,
                SandboxRunStatus.MemoryLimitExceeded => RunStatus.MemoryLimitExceeded,
                _ => RunStatus.RuntimeError
            };
            if (status == RunStatus.Completed &&
                result.MemoryUsedBytes > (long)problem.MemoryLimitKb * 1024)
            {
                status = RunStatus.MemoryLimitExceeded;
            }
            var memoryKb = (int)Math.Min(int.MaxValue, result.MemoryUsedBytes / 1024);
            await FinalizeAsync(claim, status, result.Output, result.ErrorOutput,
                result.ExecutionTimeMs, memoryKb, cancellationToken);
        }
        finally
        {
            try { if (Directory.Exists(workDirectory)) Directory.Delete(workDirectory, recursive: true); }
            catch (Exception exception) { _logger.LogWarning(exception, "Could not clean work directory for run {RunId}.", run.Id); }
        }
    }

    private string BuildSource(Problem problem, string source) => problem.ExecutionMode switch
    {
        ProblemExecutionMode.StdinStdout => source,
        ProblemExecutionMode.Function => _functionHarnessBuilder.Build(source,
            FunctionSignatureJsonSerializer.Deserialize(problem.FunctionSignatureJson ??
                throw new InvalidOperationException($"Function signature is missing for problem {problem.Id}.")),
            problem.FunctionAdapterTemplate ?? throw new InvalidOperationException(
                $"Function adapter is missing for problem {problem.Id}.")),
        _ => throw new InvalidOperationException($"Unsupported execution mode for problem {problem.Id}.")
    };

    private async Task FinalizeAsync(RunClaim claim, RunStatus status, string? stdout, string? stderr,
        int timeMs, int memoryKb, CancellationToken cancellationToken)
    {
        if (!await _runs.FinalizeClaimAsync(claim, status, stdout, stderr, timeMs, memoryKb, cancellationToken))
            throw new RunClaimLostException(claim.RunId);
    }
}
