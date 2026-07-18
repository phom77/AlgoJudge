using AlgoJudge.Application.FunctionExecution;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Application.Models.Common;
using AlgoJudge.Application.Models.RunQueue;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;
using AlgoJudge.Infrastructure.Grading;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlgoJudge.Judge.IntegrationTests;

public sealed class RunGraderServiceTests
{
    [Theory]
    [InlineData(SandboxRunStatus.Success, RunStatus.Completed)]
    [InlineData(SandboxRunStatus.TimeLimitExceeded, RunStatus.TimeLimitExceeded)]
    [InlineData(SandboxRunStatus.MemoryLimitExceeded, RunStatus.MemoryLimitExceeded)]
    [InlineData(SandboxRunStatus.RuntimeError, RunStatus.RuntimeError)]
    [InlineData(SandboxRunStatus.OutputLimitExceeded, RunStatus.RuntimeError)]
    public async Task GradeAsync_MapsSandboxResultAndReturnsCustomOutput(SandboxRunStatus sandboxStatus, RunStatus expected)
    {
        var claim = new RunClaim(Guid.NewGuid(), Guid.NewGuid(), "worker", 1, DateTime.UtcNow.AddMinutes(1));
        var runs = new RunRepositoryStub(new CodeRun { Id = claim.RunId, ProblemId = 3, SourceCode = "int main(){}", Input = "custom" });
        var sandbox = new SandboxStub { Result = new SandboxRunResult { Status = sandboxStatus, Output = "stdout", ErrorOutput = "stderr", ExecutionTimeMs = 7, MemoryUsedBytes = 4096 } };
        var service = new RunGraderService(runs, new ProblemRepositoryStub(), sandbox,
            new Cpp17FunctionHarnessBuilder(), NullLogger<RunGraderService>.Instance);

        await service.GradeAsync(claim);

        Assert.Equal(expected, runs.FinalStatus);
        Assert.Equal("stdout", runs.Stdout);
        Assert.Equal("stderr", runs.Stderr);
        Assert.Equal("custom", sandbox.Input);
        Assert.Equal(4, runs.MemoryKb);
    }

    [Fact]
    public async Task GradeAsync_CompilationFailure_ReturnsSanitizedDiagnostics()
    {
        var claim = new RunClaim(Guid.NewGuid(), Guid.NewGuid(), "worker", 1, DateTime.UtcNow.AddMinutes(1));
        var runs = new RunRepositoryStub(new CodeRun { Id = claim.RunId, ProblemId = 3, SourceCode = "bad", Input = "" });
        var sandbox = new SandboxStub { Compilation = new SandboxCompileResult { Success = false, ErrorOutput = "diagnostic" } };
        var service = new RunGraderService(runs, new ProblemRepositoryStub(), sandbox,
            new Cpp17FunctionHarnessBuilder(), NullLogger<RunGraderService>.Instance);
        await service.GradeAsync(claim);
        Assert.Equal(RunStatus.CompileError, runs.FinalStatus);
        Assert.Equal("diagnostic", runs.Stderr);
    }

    private sealed class SandboxStub : IDockerSandbox
    {
        public SandboxCompileResult Compilation { get; init; } = new() { Success = true };
        public SandboxRunResult Result { get; init; } = new() { Status = SandboxRunStatus.Success };
        public string? Input { get; private set; }
        public Task<SandboxCompileResult> CompileAsync(string sourceCode, string workDir, CancellationToken ct = default) => Task.FromResult(Compilation);
        public Task<SandboxRunResult> RunAsync(string workDir, string input, int timeLimitMs, int memoryLimitKb, CancellationToken ct = default) { Input = input; return Task.FromResult(Result); }
    }

    private sealed class ProblemRepositoryStub : IProblemRepository
    {
        private readonly Problem _problem = new() { Id = 3, TimeLimitMs = 1000, MemoryLimitKb = 262144, ExecutionMode = ProblemExecutionMode.StdinStdout };
        public Task<Problem?> GetByIdAsync(int id) => Task.FromResult<Problem?>(_problem);
        public Task<Problem?> GetPublishedBySlugAsync(string slug) => throw new NotSupportedException();
        public Task<PagedResult<Problem>> GetPublishedPagedAsync(string? search, DifficultyLevel? difficulty, IReadOnlyCollection<string> tags, Guid? userId, bool? solved, int pageNumber, int pageSize) => throw new NotSupportedException();
    }

    private sealed class RunRepositoryStub(CodeRun run) : IRunRepository
    {
        public RunStatus? FinalStatus { get; private set; } public string? Stdout { get; private set; } public string? Stderr { get; private set; } public int MemoryKb { get; private set; }
        public Task<CodeRun?> GetClaimedAsync(RunClaim claim, CancellationToken cancellationToken = default) => Task.FromResult<CodeRun?>(run);
        public Task<bool> FinalizeClaimAsync(RunClaim claim, RunStatus finalStatus, string? standardOutput, string? errorOutput, int executionTimeMs, int memoryUsedKb, CancellationToken cancellationToken = default) { FinalStatus = finalStatus; Stdout = standardOutput; Stderr = errorOutput; MemoryKb = memoryUsedKb; return Task.FromResult(true); }
        public Task AddAsync(CodeRun value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CodeRun?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RunClaim?> ClaimNextAsync(string workerId, TimeSpan leaseDuration, int maxAttempts, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> RenewLeaseAsync(RunClaim claim, TimeSpan leaseDuration, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> AbandonClaimAsync(RunClaim claim, int maxAttempts, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
