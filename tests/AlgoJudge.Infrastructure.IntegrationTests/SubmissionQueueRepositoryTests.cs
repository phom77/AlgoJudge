using AlgoJudge.Application.Models.SubmissionQueue;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;
using AlgoJudge.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AlgoJudge.Infrastructure.IntegrationTests;

public class SubmissionQueueRepositoryTests
{
    [PostgreSqlFact]
    public async Task ConcurrentWorkersClaimEachSubmissionOnlyOnce()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await SeedSubmissionsAsync(database, submissionCount: 10);

        var claimTasks = Enumerable.Range(1, 20)
            .Select(index => ClaimOnceAsync(database, $"worker-{index}"))
            .ToArray();
        var claims = (await Task.WhenAll(claimTasks))
            .Where(claim => claim is not null)
            .Cast<SubmissionClaim>()
            .ToArray();

        Assert.Equal(10, claims.Length);
        Assert.Equal(10, claims.Select(claim => claim.SubmissionId).Distinct().Count());
        Assert.All(claims, claim => Assert.Equal(1, claim.AttemptCount));
    }

    [PostgreSqlFact]
    public async Task ExpiredLeaseCanBeReclaimedAndStaleWorkerCannotFinalize()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await SeedSubmissionsAsync(database, submissionCount: 1);

        var firstClaim = await ClaimOnceAsync(
            database,
            "worker-a",
            leaseDuration: TimeSpan.FromMilliseconds(150));
        await Task.Delay(300);
        var secondClaim = await ClaimOnceAsync(
            database,
            "worker-b",
            leaseDuration: TimeSpan.FromSeconds(5));

        Assert.NotNull(firstClaim);
        Assert.NotNull(secondClaim);
        Assert.Equal(firstClaim!.SubmissionId, secondClaim!.SubmissionId);
        Assert.NotEqual(firstClaim.ClaimToken, secondClaim.ClaimToken);
        Assert.Equal(2, secondClaim.AttemptCount);

        await using var staleContext = database.CreateContext();
        var staleRepository = new SubmissionRepository(staleContext);
        Assert.False(await staleRepository.FinalizeClaimAsync(
            firstClaim,
            SubmissionStatus.Accepted,
            executionTimeMs: 10,
            memoryUsedKb: 100));

        await using var ownerContext = database.CreateContext();
        var ownerRepository = new SubmissionRepository(ownerContext);
        Assert.True(await ownerRepository.FinalizeClaimAsync(
            secondClaim,
            SubmissionStatus.Accepted,
            executionTimeMs: 10,
            memoryUsedKb: 100));
    }

    [PostgreSqlFact]
    public async Task ExhaustedExpiredClaimBecomesRuntimeError()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await SeedSubmissionsAsync(database, submissionCount: 1);

        var firstClaim = await ClaimOnceAsync(
            database,
            "worker-a",
            TimeSpan.FromMilliseconds(100),
            maxAttempts: 2);
        Assert.NotNull(firstClaim);
        await Task.Delay(250);

        var secondClaim = await ClaimOnceAsync(
            database,
            "worker-b",
            TimeSpan.FromMilliseconds(100),
            maxAttempts: 2);
        Assert.NotNull(secondClaim);
        Assert.Equal(2, secondClaim!.AttemptCount);
        await Task.Delay(250);

        var noClaim = await ClaimOnceAsync(
            database,
            "worker-c",
            TimeSpan.FromSeconds(1),
            maxAttempts: 2);
        Assert.Null(noClaim);

        await using var context = database.CreateContext();
        var submission = await context.Submissions.AsNoTracking().SingleAsync();
        Assert.Equal(SubmissionStatus.RuntimeError, submission.Status);
        Assert.NotNull(submission.FinishedAt);
        Assert.Null(submission.WorkerId);
        Assert.Null(submission.ClaimToken);
    }

    [PostgreSqlFact]
    public async Task AbandonedClaimReturnsToPendingUntilAttemptsAreExhausted()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await SeedSubmissionsAsync(database, submissionCount: 1);
        var claim = await ClaimOnceAsync(database, "worker-a");
        Assert.NotNull(claim);

        await using (var context = database.CreateContext())
        {
            var repository = new SubmissionRepository(context);
            Assert.True(await repository.AbandonClaimAsync(claim!, maxAttempts: 3));
        }

        var nextClaim = await ClaimOnceAsync(database, "worker-b");
        Assert.NotNull(nextClaim);
        Assert.Equal(2, nextClaim!.AttemptCount);

        await using (var context = database.CreateContext())
        {
            var repository = new SubmissionRepository(context);
            Assert.True(await repository.AbandonClaimAsync(nextClaim, maxAttempts: 2));
        }

        await using var verificationContext = database.CreateContext();
        var submission = await verificationContext.Submissions.AsNoTracking().SingleAsync();
        Assert.Equal(SubmissionStatus.RuntimeError, submission.Status);
        Assert.NotNull(submission.FinishedAt);
    }

    [PostgreSqlFact]
    public async Task OnlyClaimOwnerCanRenewLeaseAndRenewalPreventsReclaim()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await SeedSubmissionsAsync(database, submissionCount: 1);
        var claim = await ClaimOnceAsync(
            database,
            "worker-a",
            leaseDuration: TimeSpan.FromSeconds(1));
        Assert.NotNull(claim);

        await Task.Delay(200);
        await using (var context = database.CreateContext())
        {
            var repository = new SubmissionRepository(context);
            var staleClaim = claim! with { ClaimToken = Guid.NewGuid() };
            Assert.False(await repository.RenewLeaseAsync(
                staleClaim,
                TimeSpan.FromSeconds(1)));
            Assert.True(await repository.RenewLeaseAsync(
                claim,
                TimeSpan.FromSeconds(2)));
        }

        // The original lease has expired, but the renewed lease is still valid.
        await Task.Delay(900);
        Assert.Null(await ClaimOnceAsync(database, "worker-b"));
    }

    private static async Task<SubmissionClaim?> ClaimOnceAsync(
        PostgreSqlTestDatabase database,
        string workerId,
        TimeSpan? leaseDuration = null,
        int maxAttempts = 3)
    {
        await using var context = database.CreateContext();
        var repository = new SubmissionRepository(context);
        return await repository.ClaimNextAsync(
            workerId,
            leaseDuration ?? TimeSpan.FromSeconds(5),
            maxAttempts);
    }

    private static async Task SeedSubmissionsAsync(
        PostgreSqlTestDatabase database,
        int submissionCount)
    {
        await using var context = database.CreateContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = $"queue_user_{Guid.NewGuid():N}",
            Email = $"{Guid.NewGuid():N}@example.test",
            FullName = "Queue Test User",
            PasswordHash = "test-only"
        };
        var problem = new Problem
        {
            Slug = $"queue-problem-{Guid.NewGuid():N}",
            Title = "Queue Test Problem",
            StatementMarkdown = "Statement",
            ConstraintsMarkdown = "Constraints",
            TimeLimitMs = 1_000,
            MemoryLimitKb = 262_144,
            Difficulty = DifficultyLevel.Easy,
            Status = ProblemStatus.Published,
            PublishedAt = DateTime.UtcNow
        };
        context.AddRange(user, problem);

        for (var index = 0; index < submissionCount; index++)
        {
            context.Submissions.Add(new Submission
            {
                Id = Guid.NewGuid(),
                User = user,
                Problem = problem,
                SourceCode = "int main() {}",
                Language = "cpp17",
                Status = SubmissionStatus.Pending,
                CreatedAt = DateTime.UtcNow.AddMilliseconds(index)
            });
        }

        await context.SaveChangesAsync();
    }
}
