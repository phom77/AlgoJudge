using AlgoJudge.Application.Models.ContentGeneration;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;
using AlgoJudge.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AlgoJudge.Infrastructure.IntegrationTests;

public sealed class ContentGenerationQueueRepositoryTests
{
    [PostgreSqlFact]
    public async Task PublishCopiesReadyCandidateToNextImmutableSystemSuite()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var (revisionId, ownerId, problemId) = await SeedReadyRevisionAsync(database);
        await using (var context = database.CreateContext())
            Assert.True(await new ProblemAuthoringRepository(context).PublishAsync(revisionId, ownerId));

        await using var verify = database.CreateContext();
        var problem = await verify.Problems.SingleAsync(item => item.Id == problemId);
        var revision = await verify.ProblemAuthoringRevisions.SingleAsync(item => item.Id == revisionId);
        var test = await verify.JudgeTestCases.SingleAsync(item => item.ProblemId == problemId);
        Assert.Equal(ProblemStatus.Published, problem.Status);
        Assert.Equal(1, problem.JudgeVersion);
        Assert.Equal(AuthoringRevisionStatus.Published, revision.Status);
        Assert.Equal("{\"values\":[1]}", test.Input);
        Assert.Equal("1", test.ExpectedOutput);
    }

    [PostgreSqlFact]
    public async Task ConcurrentWorkersClaimJobOnceAndStaleWorkerCannotComplete()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var revisionId = await SeedJobAsync(database);
        var first = await ClaimAsync(database, "content-a", TimeSpan.FromMilliseconds(100));
        Assert.NotNull(first);
        await Task.Delay(250);
        var second = await ClaimAsync(database, "content-b", TimeSpan.FromSeconds(5));
        Assert.NotNull(second);
        var result = Result();
        await using (var context = database.CreateContext())
        {
            var repository = new ContentGenerationJobRepository(context);
            Assert.False(await repository.CompleteAsync(first!, result));
            Assert.True(await repository.CompleteAsync(second!, result));
        }
        await using var verify = database.CreateContext();
        var revision = await verify.ProblemAuthoringRevisions.Include(item => item.CandidateTestCases)
            .SingleAsync(item => item.Id == revisionId);
        Assert.Equal(AuthoringRevisionStatus.Ready, revision.Status);
        Assert.Single(revision.CandidateTestCases);
    }

    private static async Task<ContentGenerationClaim?> ClaimAsync(PostgreSqlTestDatabase database, string worker, TimeSpan lease)
    {
        await using var context = database.CreateContext();
        return await new ContentGenerationJobRepository(context).ClaimNextAsync(worker, lease, 3);
    }

    private static async Task<Guid> SeedJobAsync(PostgreSqlTestDatabase database)
    {
        await using var context = database.CreateContext();
        var user = new User { Id = Guid.NewGuid(), UserName = $"author_{Guid.NewGuid():N}", Email = $"{Guid.NewGuid():N}@example.test", PasswordHash = "test", FullName = "Author" };
        var problem = new Problem { Slug = $"draft-{Guid.NewGuid():N}", Title = "Draft", StatementMarkdown = "Statement", ConstraintsMarkdown = "Constraints", TimeLimitMs = 1000, MemoryLimitKb = 262144, ExecutionMode = ProblemExecutionMode.Function, FunctionSignatureJson = "{\"className\":\"Solution\",\"methodName\":\"solve\",\"returnType\":\"Int32\",\"parameters\":[]}" };
        var revision = new ProblemAuthoringRevision { Id = Guid.NewGuid(), Problem = problem, OwnerUser = user, RevisionNumber = 1, Status = AuthoringRevisionStatus.Generating, Title = "Draft", Slug = problem.Slug, StatementMarkdown = "Statement", ConstraintsMarkdown = "Constraints", Difficulty = DifficultyLevel.Easy, TimeLimitMs = 1000, MemoryLimitKb = 262144, SamplesJson = "[]", DefinitionJson = "{}", DefinitionSha256 = new string('a', 64) };
        revision.GenerationJobs.Add(new ContentGenerationJob { Id = Guid.NewGuid(), Revision = revision, Status = ContentGenerationJobStatus.Pending, DefinitionSnapshotJson = "{}", DefinitionSha256 = new string('a', 64), TimeLimitMs = 1000, MemoryLimitKb = 262144 });
        context.Add(revision); await context.SaveChangesAsync(); return revision.Id;
    }

    private static ContentGenerationResult Result() => new(new string('b', 64), "toolchain",
        [new GeneratedContentCase(1, "single", "handwritten", 0, "{}", "1", [])],
        new Dictionary<string, int> { ["handwritten"] = 1 }, 0,
        new Dictionary<string, int>(), []);

    private static async Task<(Guid RevisionId, Guid OwnerId, int ProblemId)> SeedReadyRevisionAsync(PostgreSqlTestDatabase database)
    {
        await using var context = database.CreateContext();
        var owner = new User { Id = Guid.NewGuid(), UserName = $"publisher_{Guid.NewGuid():N}", Email = $"{Guid.NewGuid():N}@example.test", PasswordHash = "test", FullName = "Publisher" };
        var problem = new Problem { Slug = $"publish-{Guid.NewGuid():N}", Title = "Draft", StatementMarkdown = "Statement", ConstraintsMarkdown = "Constraints", TimeLimitMs = 1000, MemoryLimitKb = 262144, ExecutionMode = ProblemExecutionMode.Function, FunctionSignatureJson = "{\"className\":\"Solution\",\"methodName\":\"solve\",\"returnType\":\"Int32\",\"parameters\":[]}" };
        var definition = "{\"schemaVersion\":1,\"executionMode\":\"Function\",\"functionSignature\":{\"className\":\"Solution\",\"methodName\":\"solve\",\"returnType\":\"Int32\",\"parameters\":[{\"name\":\"values\",\"type\":\"Int32Array\"}]},\"handwrittenCases\":[],\"generator\":{\"language\":\"csharp\",\"sdkVersion\":1,\"source\":\"g\"},\"inputValidator\":{\"language\":\"csharp\",\"sdkVersion\":1,\"source\":\"v\"},\"referenceSolution\":{\"language\":\"cpp17\",\"source\":\"r\"},\"wrongSolutions\":[]}";
        var revision = new ProblemAuthoringRevision { Id = Guid.NewGuid(), Problem = problem, OwnerUser = owner, RevisionNumber = 1, Status = AuthoringRevisionStatus.Ready, Title = "Published", Slug = problem.Slug, StatementMarkdown = "Statement", ConstraintsMarkdown = "Constraints", Difficulty = DifficultyLevel.Easy, TimeLimitMs = 1000, MemoryLimitKb = 262144, SamplesJson = "[{\"input\":\"{\\u0022values\\u0022:[1]}\",\"expectedOutput\":\"1\",\"explanation\":null}]", DefinitionJson = definition, DefinitionSha256 = new string('a', 64), CandidateSuiteSha256 = new string('b', 64), CandidateToolchain = "toolchain", CandidateStatisticsJson = "{}", CandidateCaseCount = 1 };
        revision.CandidateTestCases.Add(new AuthoringTestCase { Ordinal = 1, Name = "single", Group = "handwritten", Seed = 0, Input = "{\"values\":[1]}", ExpectedOutput = "1" });
        context.Add(revision); await context.SaveChangesAsync(); return (revision.Id, owner.Id, problem.Id);
    }
}
