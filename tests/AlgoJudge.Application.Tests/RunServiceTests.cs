using AlgoJudge.Application.Contracts.Runs;
using AlgoJudge.Application.Exceptions;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Application.Models.Common;
using AlgoJudge.Application.Models.RunQueue;
using AlgoJudge.Application.Services;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;
using System.Text.Json;

namespace AlgoJudge.Application.Tests;

public sealed class RunServiceTests
{
    [Fact]
    public async Task CreateAsync_StdinProblem_PersistsPendingCustomInput()
    {
        var repository = new RunRepositoryStub();
        var service = CreateService(repository, ProblemExecutionMode.StdinStdout);

        var response = await service.CreateAsync("two-sum", new CreateRunRequest
        {
            SourceCode = "int main() {}", Language = "cpp17", Input = "1 2\n"
        }, Guid.NewGuid());

        Assert.Equal(RunStatus.Pending, response.Status);
        Assert.Equal("1 2\n", repository.Added!.Input);
        Assert.Null(response.Stdout);
    }

    [Fact]
    public async Task CreateAsync_FunctionProblem_NormalizesArgumentsInSignatureOrder()
    {
        var repository = new RunRepositoryStub();
        var service = CreateService(repository, ProblemExecutionMode.Function);
        using var json = JsonDocument.Parse("{\"target\":9,\"nums\":[2,7]}");

        await service.CreateAsync("two-sum", new CreateRunRequest
        {
            SourceCode = "class Solution {};", Language = "cpp17", Arguments = json.RootElement.Clone()
        }, Guid.NewGuid());

        Assert.Equal("{\"nums\":[2,7],\"target\":9}", repository.Added!.Input);
    }

    [Fact]
    public async Task CreateAsync_FunctionProblem_RejectsWrongArgumentType()
    {
        var service = CreateService(new RunRepositoryStub(), ProblemExecutionMode.Function);
        using var json = JsonDocument.Parse("{\"nums\":\"bad\",\"target\":9}");

        await Assert.ThrowsAsync<RequestValidationException>(() => service.CreateAsync(
            "two-sum", new CreateRunRequest { SourceCode = "code", Language = "cpp17", Arguments = json.RootElement.Clone() }, Guid.NewGuid()));
    }

    [Fact]
    public async Task GetByIdAsync_RunOwnedByAnotherUser_ThrowsForbidden()
    {
        var repository = new RunRepositoryStub { Existing = true };
        var service = CreateService(repository, ProblemExecutionMode.StdinStdout);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetByIdAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    private static RunService CreateService(RunRepositoryStub runs, ProblemExecutionMode mode)
    {
        var problem = new Problem
        {
            Id = 7, Slug = "two-sum", Status = ProblemStatus.Published, ExecutionMode = mode,
            FunctionSignatureJson = "{\"className\":\"Solution\",\"methodName\":\"twoSum\",\"returnType\":\"Int32Array\",\"parameters\":[{\"name\":\"nums\",\"type\":\"Int32Array\"},{\"name\":\"target\",\"type\":\"Int32\"}]}"
        };
        return new RunService(runs, new ProblemRepositoryStub(problem), new UnitOfWorkStub());
    }

    private sealed class UnitOfWorkStub : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class ProblemRepositoryStub(Problem problem) : IProblemRepository
    {
        public Task<Problem?> GetByIdAsync(int id) => Task.FromResult<Problem?>(problem);
        public Task<Problem?> GetPublishedBySlugAsync(string slug) => Task.FromResult<Problem?>(problem);
        public Task<PagedResult<Problem>> GetPublishedPagedAsync(string? search, DifficultyLevel? difficulty,
            IReadOnlyCollection<string> tags, Guid? userId, bool? solved, int pageNumber, int pageSize) => throw new NotSupportedException();
    }

    private sealed class RunRepositoryStub : IRunRepository
    {
        public CodeRun? Added { get; private set; }
        public bool Existing { get; init; }
        public Task AddAsync(CodeRun run, CancellationToken cancellationToken = default) { Added = run; return Task.CompletedTask; }
        public Task<CodeRun?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<CodeRun?>(null);
        public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Existing);
        public Task<CodeRun?> GetClaimedAsync(RunClaim claim, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RunClaim?> ClaimNextAsync(string workerId, TimeSpan leaseDuration, int maxAttempts, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> RenewLeaseAsync(RunClaim claim, TimeSpan leaseDuration, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> FinalizeClaimAsync(RunClaim claim, RunStatus finalStatus, string? standardOutput, string? errorOutput, int executionTimeMs, int memoryUsedKb, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> AbandonClaimAsync(RunClaim claim, int maxAttempts, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
