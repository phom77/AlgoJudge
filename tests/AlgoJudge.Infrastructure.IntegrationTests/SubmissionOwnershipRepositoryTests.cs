using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;
using AlgoJudge.Infrastructure.Repositories;

namespace AlgoJudge.Infrastructure.IntegrationTests;

public class SubmissionOwnershipRepositoryTests
{
    [PostgreSqlFact]
    public async Task DetailLookupMaterializesOnlyTheOwnersSubmission()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();
        await SeedSubmissionAsync(database, ownerId, otherUserId, submissionId);

        await using var context = database.CreateContext();
        var repository = new SubmissionRepository(context);

        var denied = await repository.GetByIdForUserAsync(submissionId, otherUserId);

        Assert.Null(denied);
        Assert.Empty(context.ChangeTracker.Entries<Submission>());
        Assert.True(await repository.ExistsAsync(submissionId));

        var owned = await repository.GetByIdForUserAsync(submissionId, ownerId);
        Assert.NotNull(owned);
        Assert.Equal(ownerId, owned!.UserId);
        Assert.Empty(context.ChangeTracker.Entries<Submission>());
    }

    private static async Task SeedSubmissionAsync(
        PostgreSqlTestDatabase database,
        Guid ownerId,
        Guid otherUserId,
        Guid submissionId)
    {
        await using var context = database.CreateContext();
        var owner = CreateUser(ownerId, "owner");
        var otherUser = CreateUser(otherUserId, "other");
        var problem = new Problem
        {
            Slug = $"ownership-{Guid.NewGuid():N}",
            Title = "Ownership test",
            StatementMarkdown = "Statement",
            ConstraintsMarkdown = "Constraints",
            TimeLimitMs = 1_000,
            MemoryLimitKb = 262_144,
            Difficulty = DifficultyLevel.Easy,
            Status = ProblemStatus.Published,
            PublishedAt = DateTime.UtcNow
        };
        context.AddRange(owner, otherUser, problem);
        context.Submissions.Add(new Submission
        {
            Id = submissionId,
            User = owner,
            Problem = problem,
            SourceCode = "private-source-sentinel",
            Language = "cpp17",
            Status = SubmissionStatus.Pending,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
    }

    private static User CreateUser(Guid id, string prefix)
    {
        return new User
        {
            Id = id,
            UserName = $"{prefix}_{Guid.NewGuid():N}",
            Email = $"{prefix}_{Guid.NewGuid():N}@example.test",
            FullName = $"{prefix} user",
            PasswordHash = "test-only"
        };
    }
}
