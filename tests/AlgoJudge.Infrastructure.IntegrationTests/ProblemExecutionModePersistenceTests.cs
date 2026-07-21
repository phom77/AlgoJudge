using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AlgoJudge.Infrastructure.IntegrationTests;

public sealed class ProblemExecutionModePersistenceTests
{
    [PostgreSqlFact]
    public async Task PostgreSqlEnforcesFunctionConfigurationInvariant()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using (var context = database.CreateContext())
        {
            context.Problems.Add(CreateProblem(
                "valid-function",
                ProblemExecutionMode.Function,
                "{\"className\":\"Solution\"}",
                "{{USER_SOURCE}} {{CLASS_NAME}} {{METHOD_NAME}}"));
            await context.SaveChangesAsync();
        }

        await using (var verificationContext = database.CreateContext())
        {
            var persisted = await verificationContext.Problems
                .AsNoTracking()
                .SingleAsync(problem => problem.Slug == "valid-function");
            Assert.Equal(ProblemExecutionMode.Function, persisted.ExecutionMode);
            Assert.NotNull(persisted.FunctionSignatureJson);
            Assert.NotNull(persisted.FunctionAdapterTemplate);
        }

        await using var invalidContext = database.CreateContext();
        invalidContext.Problems.Add(CreateProblem(
            "invalid-function",
            ProblemExecutionMode.Function,
            signature: null,
            adapter: null));
        await Assert.ThrowsAsync<DbUpdateException>(() => invalidContext.SaveChangesAsync());
    }

    [PostgreSqlFact]
    public async Task PostgreSqlAllowsFunctionProblemToUseGenericHarness()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using (var context = database.CreateContext())
        {
            context.Problems.Add(CreateProblem(
                "generic-function",
                ProblemExecutionMode.Function,
                "{\"className\":\"Solution\"}",
                adapter: null));
            await context.SaveChangesAsync();
        }

        await using var verificationContext = database.CreateContext();
        var persisted = await verificationContext.Problems
            .AsNoTracking()
            .SingleAsync(problem => problem.Slug == "generic-function");
        Assert.Equal(ProblemExecutionMode.Function, persisted.ExecutionMode);
        Assert.NotNull(persisted.FunctionSignatureJson);
        Assert.Null(persisted.FunctionAdapterTemplate);
    }

    private static Problem CreateProblem(
        string slug,
        ProblemExecutionMode mode,
        string? signature,
        string? adapter) => new()
        {
            Slug = slug,
            Title = "Execution Mode Test",
            StatementMarkdown = "Statement",
            ConstraintsMarkdown = "Constraints",
            TimeLimitMs = 1_000,
            MemoryLimitKb = 262_144,
            Difficulty = DifficultyLevel.Easy,
            ExecutionMode = mode,
            FunctionSignatureJson = signature,
            FunctionAdapterTemplate = adapter,
            Status = ProblemStatus.Draft
        };
}
