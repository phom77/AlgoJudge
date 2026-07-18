using AlgoJudge.Application.FunctionExecution;
using AlgoJudge.ContentTool.Importing;
using AlgoJudge.ContentTool.Packages;
using AlgoJudge.Domain.Enums;
using AlgoJudge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AlgoJudge.ContentTool.Tests;

public class ProblemPackageImporterTests
{
    [Fact]
    public async Task ImportCreatesCompleteDraftProblem()
    {
        await using var database = TestDatabase.Create();
        var importer = new ProblemPackageImporter(database.Context);

        var result = await importer.ImportAsync(CreatePackage(), replace: false);

        database.Context.ChangeTracker.Clear();
        var problem = await database.Context.Problems
            .Include(item => item.Samples)
            .Include(item => item.JudgeTestCases)
            .Include(item => item.Tags)
                .ThenInclude(problemTag => problemTag.Tag)
            .SingleAsync();

        Assert.False(result.Replaced);
        Assert.Equal(ProblemStatus.Draft, problem.Status);
        Assert.Equal(1, problem.JudgeVersion);
        Assert.Single(problem.Samples);
        Assert.Equal(2, problem.JudgeTestCases.Count);
        Assert.All(problem.JudgeTestCases, testCase =>
            Assert.Equal(problem.JudgeVersion, testCase.SystemTestSuiteVersion));
        Assert.Equal(
            new[] { "array", "hash-table" },
            problem.Tags.Select(problemTag => problemTag.Tag.Slug).OrderBy(slug => slug));
    }

    [Fact]
    public async Task DuplicateSlugIsRejectedWithoutReplace()
    {
        await using var database = TestDatabase.Create();
        var importer = new ProblemPackageImporter(database.Context);
        var package = CreatePackage();
        await importer.ImportAsync(package, replace: false);

        await Assert.ThrowsAsync<ContentImportConflictException>(() =>
            importer.ImportAsync(package, replace: false));

        Assert.Equal(1, await database.Context.Problems.CountAsync());
    }

    [Fact]
    public async Task ReplaceUpdatesOnlyDraftContentAndIncrementsJudgeVersion()
    {
        await using var database = TestDatabase.Create();
        var importer = new ProblemPackageImporter(database.Context);
        await importer.ImportAsync(CreatePackage(), replace: false);

        var replacement = CreatePackage(
            title: "Two Sum Updated",
            judgeTestCases:
            [
                new ProblemPackageJudgeTestCase(1, "replacement-input", "replacement-output")
            ]);
        var result = await importer.ImportAsync(replacement, replace: true);

        database.Context.ChangeTracker.Clear();
        var problem = await database.Context.Problems
            .Include(item => item.JudgeTestCases)
            .SingleAsync();

        Assert.True(result.Replaced);
        Assert.Equal("Two Sum Updated", problem.Title);
        Assert.Equal(2, problem.JudgeVersion);
        var replacementCase = Assert.Single(problem.JudgeTestCases);
        Assert.Equal("replacement-input", replacementCase.Input);
        Assert.Equal(2, replacementCase.SystemTestSuiteVersion);
    }

    [Fact]
    public async Task ReplaceCannotOverwritePublishedProblem()
    {
        await using var database = TestDatabase.Create();
        var importer = new ProblemPackageImporter(database.Context);
        await importer.ImportAsync(CreatePackage(), replace: false);

        var problem = await database.Context.Problems.SingleAsync();
        problem.Status = ProblemStatus.Published;
        problem.PublishedAt = DateTime.UtcNow;
        await database.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<ContentImportConflictException>(() =>
            importer.ImportAsync(CreatePackage(title: "Forbidden update"), replace: true));

        database.Context.ChangeTracker.Clear();
        Assert.Equal("Two Sum", (await database.Context.Problems.SingleAsync()).Title);
    }

    [Fact]
    public async Task ImportPersistsFunctionExecutionConfiguration()
    {
        await using var database = TestDatabase.Create();
        var importer = new ProblemPackageImporter(database.Context);
        var function = new ProblemPackageFunction(
            new FunctionSignature
            {
                ClassName = "Solution",
                MethodName = "solve",
                ReturnType = FunctionValueType.Int32,
                Parameters =
                [
                    new FunctionParameter { Name = "value", Type = FunctionValueType.Int32 }
                ]
            },
            "{\"className\":\"Solution\",\"methodName\":\"solve\",\"returnType\":\"Int32\",\"parameters\":[{\"name\":\"value\",\"type\":\"Int32\"}]}",
            "{{USER_SOURCE}} {{CLASS_NAME}} instance; // {{METHOD_NAME}}");

        await importer.ImportAsync(
            CreatePackage(
                executionMode: ProblemExecutionMode.Function,
                function: function),
            replace: false);

        database.Context.ChangeTracker.Clear();
        var problem = await database.Context.Problems.SingleAsync();
        Assert.Equal(ProblemExecutionMode.Function, problem.ExecutionMode);
        Assert.Equal(function.SignatureJson, problem.FunctionSignatureJson);
        Assert.Equal(function.AdapterTemplate, problem.FunctionAdapterTemplate);
    }

    private static ProblemPackage CreatePackage(
        string title = "Two Sum",
        IReadOnlyCollection<ProblemPackageJudgeTestCase>? judgeTestCases = null,
        ProblemExecutionMode executionMode = ProblemExecutionMode.StdinStdout,
        ProblemPackageFunction? function = null)
    {
        return new ProblemPackage(
            new ProblemPackageMetadata
            {
                SchemaVersion = 1,
                Slug = "two-sum",
                Title = title,
                Difficulty = DifficultyLevel.Easy,
                TimeLimitMs = 1_000,
                MemoryLimitKb = 262_144,
                ExecutionMode = executionMode,
                Tags =
                [
                    new ProblemPackageTag { Slug = "array", Name = "Array" },
                    new ProblemPackageTag { Slug = "hash-table", Name = "Hash Table" }
                ]
            },
            "# Two Sum",
            "- At least two values.",
            [new ProblemPackageSample(1, "sample-input", "sample-output", null)],
            judgeTestCases ??
            [
                new ProblemPackageJudgeTestCase(1, "test-input-1", "test-output-1"),
                new ProblemPackageJudgeTestCase(2, "test-input-2", "test-output-2")
            ],
            function);
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private TestDatabase(AppDbContext context)
        {
            Context = context;
        }

        public AppDbContext Context { get; }

        public static TestDatabase Create()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"content-import-{Guid.NewGuid():N}")
                .ConfigureWarnings(warnings =>
                    warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            var context = new AppDbContext(options);
            return new TestDatabase(context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
        }
    }
}
