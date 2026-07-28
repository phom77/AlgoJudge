using AlgoJudge.Application.Models.ContentGeneration;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;
using AlgoJudge.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AlgoJudge.Infrastructure.IntegrationTests;

public sealed class ContentGenerationQueueRepositoryTests
{
    [PostgreSqlFact]
    public async Task AddGenerationJobPersistsJobWithPreassignedGuid()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var revisionId = await SeedDraftRevisionAsync(database);
        var jobId = Guid.NewGuid();

        await using (var context = database.CreateContext())
        {
            var repository = new ProblemAuthoringRepository(context);
            await repository.AddGenerationJobAsync(new ContentGenerationJob
            {
                Id = jobId,
                RevisionId = revisionId,
                Status = ContentGenerationJobStatus.Pending,
                DefinitionSnapshotJson = "{}",
                DefinitionSha256 = new string('a', 64),
                TimeLimitMs = 1000,
                MemoryLimitKb = 262144
            });
            await context.SaveChangesAsync();
        }

        await using var verify = database.CreateContext();
        var persisted = await verify.ContentGenerationJobs.SingleAsync(item => item.Id == jobId);
        Assert.Equal(revisionId, persisted.RevisionId);
        Assert.Equal(ContentGenerationJobStatus.Pending, persisted.Status);
    }

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
        var suite = await verify.SystemTestSuites.SingleAsync(item => item.ProblemId == problemId);
        Assert.Equal(ProblemStatus.Published, problem.Status);
        Assert.Equal(1, problem.JudgeVersion);
        Assert.Equal(AuthoringRevisionStatus.Published, revision.Status);
        Assert.Equal("{\"values\":[1]}", test.Input);
        Assert.Equal("1", test.ExpectedOutput);
        Assert.Equal(OutputCheckerKind.JsonExact, suite.OutputCheckerKind);
    }

    [PostgreSqlFact]
    public async Task PublishRejectsReadyCandidateThatNoLongerMeetsItsQualityPolicy()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var (revisionId, ownerId, problemId) = await SeedReadyRevisionAsync(database);
        await using (var context = database.CreateContext())
        {
            var revision = await context.ProblemAuthoringRevisions.SingleAsync(item => item.Id == revisionId);
            revision.DefinitionJson = revision.DefinitionJson[..^1] +
                ",\"qualityPolicy\":{\"minimumTestCaseCount\":2}}";
            await context.SaveChangesAsync();
        }

        await using (var context = database.CreateContext())
            Assert.False(await new ProblemAuthoringRepository(context).PublishAsync(revisionId, ownerId));

        await using var verify = database.CreateContext();
        Assert.Empty(await verify.SystemTestSuites.Where(item => item.ProblemId == problemId).ToListAsync());
        Assert.Empty(await verify.JudgeTestCases.Where(item => item.ProblemId == problemId).ToListAsync());
        Assert.Equal(ProblemStatus.Draft,
            await verify.Problems.Where(item => item.Id == problemId).Select(item => item.Status).SingleAsync());
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

    [PostgreSqlFact]
    public async Task BatchJobCompletionUsesLeaseFencingAndCheckpointsItemAndAudit()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var (batchId, itemId) = await SeedBatchJobAsync(database);
        var first = await ClaimAsync(
            database,
            "batch-content-a",
            TimeSpan.FromMilliseconds(100));
        Assert.NotNull(first);
        await Task.Delay(250);
        var second = await ClaimAsync(
            database,
            "batch-content-b",
            TimeSpan.FromSeconds(5));
        Assert.NotNull(second);

        await using (var context = database.CreateContext())
        {
            var repository = new ContentGenerationJobRepository(context);
            Assert.False(await repository.CompleteAsync(first!, Result()));
            Assert.True(await repository.CompleteAsync(second!, Result()));
        }

        await using var verify = database.CreateContext();
        var item = await verify.ContentBatchItems.SingleAsync(value => value.Id == itemId);
        var batch = await verify.ContentBatches.SingleAsync(value => value.Id == batchId);
        var audits = await verify.ContentBatchAuditEntries
            .Where(value => value.BatchId == batchId)
            .ToArrayAsync();
        Assert.Equal(ContentBatchItemStatus.Ready, item.Status);
        Assert.Equal(ContentBatchStatus.ReadyForReview, batch.Status);
        var audit = Assert.Single(audits);
        Assert.Equal("generate", audit.Action);
        Assert.Equal("succeeded", audit.Result);
        Assert.Null(audit.SafeFailureCategory);
    }

    [PostgreSqlFact]
    public async Task BatchJobIsNotClaimableUntilImportCheckpointsAreComplete()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var (batchId, _) = await SeedBatchJobAsync(database);
        await using (var context = database.CreateContext())
        {
            var batch = await context.ContentBatches.SingleAsync(value => value.Id == batchId);
            batch.Status = ContentBatchStatus.Validating;
            await context.SaveChangesAsync();
        }

        Assert.Null(await ClaimAsync(
            database,
            "batch-content-waiting",
            TimeSpan.FromSeconds(5)));

        await using (var context = database.CreateContext())
        {
            var batch = await context.ContentBatches.SingleAsync(value => value.Id == batchId);
            batch.Status = ContentBatchStatus.Generating;
            await context.SaveChangesAsync();
        }

        Assert.NotNull(await ClaimAsync(
            database,
            "batch-content-ready",
            TimeSpan.FromSeconds(5)));
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

    private static async Task<(Guid BatchId, Guid ItemId)> SeedBatchJobAsync(
        PostgreSqlTestDatabase database)
    {
        await using var context = database.CreateContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = $"batch_author_{Guid.NewGuid():N}",
            Email = $"{Guid.NewGuid():N}@example.test",
            PasswordHash = "test",
            FullName = "Batch Author",
            Role = UserRole.Admin
        };
        var problem = new Problem
        {
            Slug = $"batch-draft-{Guid.NewGuid():N}",
            Title = "Batch Draft",
            StatementMarkdown = "Statement",
            ConstraintsMarkdown = "Constraints",
            TimeLimitMs = 1_000,
            MemoryLimitKb = 262_144,
            ExecutionMode = ProblemExecutionMode.Function,
            FunctionSignatureJson =
                "{\"className\":\"Solution\",\"methodName\":\"solve\",\"returnType\":\"Int32\",\"parameters\":[]}"
        };
        var revision = new ProblemAuthoringRevision
        {
            Id = Guid.NewGuid(),
            Problem = problem,
            OwnerUser = user,
            RevisionNumber = 1,
            Status = AuthoringRevisionStatus.Generating,
            Title = problem.Title,
            Slug = problem.Slug,
            StatementMarkdown = problem.StatementMarkdown,
            ConstraintsMarkdown = problem.ConstraintsMarkdown,
            Difficulty = DifficultyLevel.Easy,
            TimeLimitMs = problem.TimeLimitMs,
            MemoryLimitKb = problem.MemoryLimitKb,
            SamplesJson = "[]",
            DefinitionJson = "{}",
            DefinitionSha256 = new string('a', 64),
            ContentHash = new string('b', 64)
        };
        var batch = new ContentBatch
        {
            Id = Guid.NewGuid(),
            CreatedByUser = user,
            CreatedByUserId = user.Id,
            CatalogName = "catalog.json",
            Status = ContentBatchStatus.Generating
        };
        var item = new ContentBatchItem
        {
            Id = Guid.NewGuid(),
            Batch = batch,
            BatchId = batch.Id,
            Ordinal = 1,
            CatalogPath = $"problems/{problem.Slug}/problem.json",
            Action = ContentBatchImportAction.Create,
            Status = ContentBatchItemStatus.Generating,
            ContentHash = revision.ContentHash,
            Slug = problem.Slug,
            Title = problem.Title,
            StatementMarkdown = problem.StatementMarkdown,
            ConstraintsMarkdown = problem.ConstraintsMarkdown,
            Difficulty = problem.Difficulty,
            TimeLimitMs = problem.TimeLimitMs,
            MemoryLimitKb = problem.MemoryLimitKb,
            TagsJson = "[]",
            SamplesJson = "[]",
            DefinitionJson = "{}",
            GeneratorParametersJson = "{}",
            Problem = problem,
            Revision = revision
        };
        revision.GenerationJobs.Add(new ContentGenerationJob
        {
            Id = Guid.NewGuid(),
            Revision = revision,
            BatchItem = item,
            Status = ContentGenerationJobStatus.Pending,
            DefinitionSnapshotJson = "{}",
            DefinitionSha256 = new string('a', 64),
            TimeLimitMs = 1_000,
            MemoryLimitKb = 262_144
        });
        context.Add(item);
        await context.SaveChangesAsync();
        return (batch.Id, item.Id);
    }

    private static async Task<Guid> SeedDraftRevisionAsync(PostgreSqlTestDatabase database)
    {
        await using var context = database.CreateContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = $"draft_author_{Guid.NewGuid():N}",
            Email = $"{Guid.NewGuid():N}@example.test",
            PasswordHash = "test",
            FullName = "Draft Author"
        };
        var problem = new Problem
        {
            Slug = $"draft-job-{Guid.NewGuid():N}",
            Title = "Draft",
            StatementMarkdown = "Statement",
            ConstraintsMarkdown = "Constraints",
            TimeLimitMs = 1000,
            MemoryLimitKb = 262144,
            ExecutionMode = ProblemExecutionMode.Function,
            FunctionSignatureJson =
                "{\"className\":\"Solution\",\"methodName\":\"solve\",\"returnType\":\"Int32\",\"parameters\":[]}"
        };
        var revision = new ProblemAuthoringRevision
        {
            Id = Guid.NewGuid(),
            Problem = problem,
            OwnerUser = user,
            RevisionNumber = 1,
            Status = AuthoringRevisionStatus.Draft,
            Title = "Draft",
            Slug = problem.Slug,
            StatementMarkdown = "Statement",
            ConstraintsMarkdown = "Constraints",
            Difficulty = DifficultyLevel.Easy,
            TimeLimitMs = 1000,
            MemoryLimitKb = 262144,
            SamplesJson = "[]",
            DefinitionJson = "{}",
            DefinitionSha256 = new string('a', 64)
        };
        context.Add(revision);
        await context.SaveChangesAsync();
        return revision.Id;
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
