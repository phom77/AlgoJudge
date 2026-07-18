using AlgoJudge.ContentTool.Publishing;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;
using AlgoJudge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlgoJudge.ContentTool.Tests;

public class ProblemPublicationServiceTests
{
    private static readonly DateTimeOffset PublishedAt =
        new(2026, 7, 18, 1, 2, 3, TimeSpan.Zero);

    [Fact]
    public async Task PublishMakesCompleteDraftPublic()
    {
        await using var context = CreateContext();
        context.Problems.Add(CreateCompleteProblem());
        await context.SaveChangesAsync();
        var service = new ProblemPublicationService(context, new FixedTimeProvider(PublishedAt));

        var result = await service.PublishAsync("two-sum");

        Assert.True(result.Changed);
        Assert.Equal(ProblemStatus.Published, result.Status);
        var problem = await context.Problems.SingleAsync();
        Assert.Equal(ProblemStatus.Published, problem.Status);
        Assert.Equal(PublishedAt.UtcDateTime, problem.PublishedAt);
        Assert.Equal(PublishedAt.UtcDateTime, problem.UpdatedAt);
    }

    [Fact]
    public async Task PublishIsIdempotentForPublishedProblem()
    {
        await using var context = CreateContext();
        var problem = CreateCompleteProblem();
        problem.Status = ProblemStatus.Published;
        problem.PublishedAt = PublishedAt.UtcDateTime;
        context.Problems.Add(problem);
        await context.SaveChangesAsync();
        var service = new ProblemPublicationService(context);

        var result = await service.PublishAsync("two-sum");

        Assert.False(result.Changed);
        Assert.Equal(PublishedAt.UtcDateTime, problem.PublishedAt);
    }

    [Theory]
    [InlineData(true, false, "public sample")]
    [InlineData(false, true, "private judge case")]
    public async Task PublishRejectsIncompleteJudgeContent(
        bool removeSamples,
        bool removeJudgeCases,
        string expectedMessage)
    {
        await using var context = CreateContext();
        var problem = CreateCompleteProblem();
        if (removeSamples) problem.Samples.Clear();
        if (removeJudgeCases) problem.JudgeTestCases.Clear();
        context.Problems.Add(problem);
        await context.SaveChangesAsync();
        var service = new ProblemPublicationService(context);

        var exception = await Assert.ThrowsAsync<ContentPublicationConflictException>(() =>
            service.PublishAsync("two-sum"));

        Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
        Assert.Equal(ProblemStatus.Draft, problem.Status);
    }

    [Fact]
    public async Task UnpublishReturnsPublishedProblemToDraft()
    {
        await using var context = CreateContext();
        var problem = CreateCompleteProblem();
        problem.Status = ProblemStatus.Published;
        problem.PublishedAt = DateTime.UtcNow;
        context.Problems.Add(problem);
        await context.SaveChangesAsync();
        var service = new ProblemPublicationService(context, new FixedTimeProvider(PublishedAt));

        var result = await service.UnpublishAsync("two-sum");

        Assert.True(result.Changed);
        Assert.Equal(ProblemStatus.Draft, problem.Status);
        Assert.Null(problem.PublishedAt);
        Assert.Equal(PublishedAt.UtcDateTime, problem.UpdatedAt);
    }

    [Fact]
    public async Task PublishRejectsFunctionProblemWithoutValidConfiguration()
    {
        await using var context = CreateContext();
        var problem = CreateCompleteProblem();
        problem.ExecutionMode = ProblemExecutionMode.Function;
        context.Problems.Add(problem);
        await context.SaveChangesAsync();
        var service = new ProblemPublicationService(context);

        var exception = await Assert.ThrowsAsync<ContentPublicationConflictException>(() =>
            service.PublishAsync("two-sum"));

        Assert.Contains("function configuration", exception.Message, StringComparison.Ordinal);
        Assert.Equal(ProblemStatus.Draft, problem.Status);
    }

    [Fact]
    public async Task PublishAcceptsValidatedFunctionConfiguration()
    {
        await using var context = CreateContext();
        var problem = CreateCompleteProblem();
        problem.ExecutionMode = ProblemExecutionMode.Function;
        problem.FunctionSignatureJson =
            "{\"className\":\"Solution\",\"methodName\":\"solve\"," +
            "\"returnType\":\"Int32\",\"parameters\":[{" +
            "\"name\":\"value\",\"type\":\"Int32\"}]}";
        problem.FunctionAdapterTemplate =
            "{{USER_SOURCE}} {{CLASS_NAME}} instance; // {{METHOD_NAME}}";
        problem.Samples.Single().Input = "{\"value\":1}";
        problem.Samples.Single().ExpectedOutput = "1";
        problem.JudgeTestCases.Single().Input = "{\"value\":2}";
        problem.JudgeTestCases.Single().ExpectedOutput = "2";
        context.Problems.Add(problem);
        await context.SaveChangesAsync();
        var service = new ProblemPublicationService(context);

        var result = await service.PublishAsync("two-sum");

        Assert.True(result.Changed);
        Assert.Equal(ProblemStatus.Published, result.Status);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"content-publish-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static Problem CreateCompleteProblem()
    {
        return new Problem
        {
            Slug = "two-sum",
            Title = "Two Sum",
            StatementMarkdown = "Find two indices.",
            ConstraintsMarkdown = "At least two values.",
            Difficulty = DifficultyLevel.Easy,
            TimeLimitMs = 1_000,
            MemoryLimitKb = 262_144,
            JudgeVersion = 1,
            Status = ProblemStatus.Draft,
            Samples =
            [
                new ProblemSample { Ordinal = 1, Input = "sample", ExpectedOutput = "0 1" }
            ],
            JudgeTestCases =
            [
                new JudgeTestCase { Ordinal = 1, Input = "private", ExpectedOutput = "0 1" }
            ]
        };
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
