using AlgoJudge.Application.Models.RunQueue;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;
using AlgoJudge.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AlgoJudge.Infrastructure.IntegrationTests;

public sealed class RunQueueRepositoryTests
{
    [PostgreSqlFact]
    public async Task ConcurrentWorkersClaimEachRunOnlyOnce()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await SeedRunsAsync(database, 6);
        var claims = (await Task.WhenAll(Enumerable.Range(1, 12)
            .Select(index => ClaimAsync(database, $"run-worker-{index}"))))
            .Where(claim => claim is not null).Cast<RunClaim>().ToArray();

        Assert.Equal(6, claims.Length);
        Assert.Equal(6, claims.Select(claim => claim.RunId).Distinct().Count());
    }

    [PostgreSqlFact]
    public async Task ReclaimedRunRejectsStaleFinalizationAndStoresOwnerOutput()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var (runId, ownerId) = await SeedRunsAsync(database, 1);
        var first = await ClaimAsync(database, "worker-a", TimeSpan.FromMilliseconds(100));
        await Task.Delay(250);
        var second = await ClaimAsync(database, "worker-b", TimeSpan.FromSeconds(5));

        Assert.NotNull(first); Assert.NotNull(second);
        await using (var context = database.CreateContext())
        {
            var repository = new RunRepository(context);
            Assert.False(await repository.FinalizeClaimAsync(first!, RunStatus.Completed, "stale", null, 1, 1));
            Assert.True(await repository.FinalizeClaimAsync(second!, RunStatus.Completed, "answer", "", 5, 12));
        }
        await using var verify = database.CreateContext();
        var repositoryForRead = new RunRepository(verify);
        var run = await repositoryForRead.GetByIdForUserAsync(runId, ownerId);
        Assert.Equal(RunStatus.Completed, run!.Status);
        Assert.Equal("answer", run.StandardOutput);
        Assert.Null(await repositoryForRead.GetByIdForUserAsync(runId, Guid.NewGuid()));
    }

    private static async Task<RunClaim?> ClaimAsync(PostgreSqlTestDatabase database, string worker,
        TimeSpan? lease = null)
    {
        await using var context = database.CreateContext();
        return await new RunRepository(context).ClaimNextAsync(worker, lease ?? TimeSpan.FromSeconds(5), 3);
    }

    private static async Task<(Guid RunId, Guid OwnerId)> SeedRunsAsync(PostgreSqlTestDatabase database, int count)
    {
        await using var context = database.CreateContext();
        var user = new User { Id = Guid.NewGuid(), UserName = $"run_{Guid.NewGuid():N}", Email = $"{Guid.NewGuid():N}@example.test", FullName = "Run User", PasswordHash = "test-only" };
        var problem = new Problem { Slug = $"run-{Guid.NewGuid():N}", Title = "Run Problem", StatementMarkdown = "Statement", ConstraintsMarkdown = "Constraints", TimeLimitMs = 1000, MemoryLimitKb = 262144, Difficulty = DifficultyLevel.Easy, Status = ProblemStatus.Published, PublishedAt = DateTime.UtcNow };
        context.AddRange(user, problem);
        Guid firstId = default;
        for (var index = 0; index < count; index++)
        {
            var run = new CodeRun { Id = Guid.NewGuid(), User = user, Problem = problem, SourceCode = "int main() {}", Language = "cpp17", Input = "", Status = RunStatus.Pending, CreatedAt = DateTime.UtcNow.AddMilliseconds(index) };
            if (index == 0) firstId = run.Id;
            context.CodeRuns.Add(run);
        }
        await context.SaveChangesAsync();
        return (firstId, user.Id);
    }
}
