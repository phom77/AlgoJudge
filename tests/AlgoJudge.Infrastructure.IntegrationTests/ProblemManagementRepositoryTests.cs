using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;
using AlgoJudge.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AlgoJudge.Infrastructure.IntegrationTests;

public sealed class ProblemManagementRepositoryTests
{
    [PostgreSqlFact]
    public async Task ListsAllStatusesAndAtomicallyArchivesPublishedProblem()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var publishedId = 0;
        await using (var context = database.CreateContext())
        {
            var published = CreateProblem("published-problem", ProblemStatus.Published);
            context.Problems.AddRange(published, CreateProblem("draft-problem", ProblemStatus.Draft));
            await context.SaveChangesAsync();
            publishedId = published.Id;
        }

        await using (var context = database.CreateContext())
        {
            var repository = new ProblemManagementRepository(context);
            var changed = await repository.TransitionStatusAsync(
                publishedId,
                ProblemStatus.Published,
                ProblemStatus.Archived);
            var archived = await repository.GetPagedAsync(
                search: null,
                status: ProblemStatus.Archived,
                pageNumber: 1,
                pageSize: 20);

            Assert.True(changed);
            var item = Assert.Single(archived.Items);
            Assert.Equal(publishedId, item.Id);
            Assert.Equal(ProblemStatus.Archived, item.Status);
        }

        await using var verificationContext = database.CreateContext();
        var persisted = await verificationContext.Problems.SingleAsync(problem => problem.Id == publishedId);
        Assert.Equal(ProblemStatus.Archived, persisted.Status);
    }

    private static Problem CreateProblem(string slug, ProblemStatus status) => new()
    {
        Slug = slug,
        Title = slug,
        StatementMarkdown = "Statement",
        ConstraintsMarkdown = "Constraints",
        Difficulty = DifficultyLevel.Easy,
        ExecutionMode = ProblemExecutionMode.StdinStdout,
        TimeLimitMs = 1_000,
        MemoryLimitKb = 262_144,
        Status = status,
        PublishedAt = status == ProblemStatus.Published ? DateTime.UtcNow : null
    };
}
