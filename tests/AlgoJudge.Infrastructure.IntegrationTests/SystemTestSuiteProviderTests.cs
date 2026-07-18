using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;
using AlgoJudge.Infrastructure.Grading;

namespace AlgoJudge.Infrastructure.IntegrationTests;

public sealed class SystemTestSuiteProviderTests
{
    [PostgreSqlFact]
    public async Task ProviderReturnsOnlyTheRequestedVersionInOrdinalOrder()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        int problemId;
        await using (var context = database.CreateContext())
        {
            var problem = new Problem
            {
                Slug = $"suite-{Guid.NewGuid():N}", Title = "Suite", StatementMarkdown = "Statement",
                ConstraintsMarkdown = "Constraints", TimeLimitMs = 1000, MemoryLimitKb = 262144,
                Difficulty = DifficultyLevel.Easy, Status = ProblemStatus.Published,
                JudgeVersion = 2, PublishedAt = DateTime.UtcNow
            };
            problem.JudgeTestCases.Add(new JudgeTestCase { SystemTestSuiteVersion = 1, Ordinal = 1, Input = "old", ExpectedOutput = "old" });
            problem.JudgeTestCases.Add(new JudgeTestCase { SystemTestSuiteVersion = 2, Ordinal = 2, Input = "second", ExpectedOutput = "2" });
            problem.JudgeTestCases.Add(new JudgeTestCase { SystemTestSuiteVersion = 2, Ordinal = 1, Input = "first", ExpectedOutput = "1" });
            context.Problems.Add(problem);
            await context.SaveChangesAsync();
            problemId = problem.Id;
        }

        await using var readContext = database.CreateContext();
        var suite = await new PostgreSqlSystemTestSuiteProvider(readContext)
            .GetSystemSuiteAsync(problemId, 2);

        Assert.NotNull(suite);
        Assert.Equal(2, suite.Version);
        Assert.Equal(["first", "second"], suite.TestCases.Select(testCase => testCase.Input));
        Assert.DoesNotContain(suite.TestCases, testCase => testCase.Input == "old");
    }
}
