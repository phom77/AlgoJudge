using System.Text.Json;
using AlgoJudge.Application.ContentGeneration;
using AlgoJudge.Application.Contracts.Admin;
using AlgoJudge.Application.FunctionExecution;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Application.Models.Common;
using AlgoJudge.Application.Services;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Application.Tests;

public sealed class ContentBatchServiceTests
{
    [Fact]
    public async Task CreateKeepsInvalidAndDuplicateItemsAsIndependentFailures()
    {
        var fixture = new Fixture();
        var first = ValidItem("duplicate");
        var second = ValidItem("duplicate");
        var invalid = ValidItem("invalid");
        invalid = Copy(invalid, validationFailureCategory: "invalid_path");

        var batch = await fixture.Service.CreateAsync(
            fixture.AdminId,
            new CreateContentBatchRequest
            {
                CatalogName = "catalog.json",
                Items = [first, second, invalid]
            });

        Assert.Equal(ContentBatchStatus.Created, batch.Status);
        Assert.Equal(3, batch.Counts.Failed);
        Assert.Equal(
            ["duplicate_slug", "duplicate_slug", "invalid_path"],
            batch.Items.Select(item => item.SafeFailureCategory!).ToArray());
        Assert.All(batch.AuditEntries, entry =>
        {
            Assert.DoesNotContain("generator", entry.Result, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("reference", entry.Result, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("C:/outside")]
    [InlineData("/absolute")]
    [InlineData("problems\\unsafe")]
    public async Task CreateRejectsUnsafeCatalogPaths(string catalogPath)
    {
        var fixture = new Fixture();
        var request = Copy(
            ValidItem($"unsafe-{Guid.NewGuid():N}"),
            validationFailureCategory: null,
            catalogPath: catalogPath);

        var batch = await fixture.Service.CreateAsync(
            fixture.AdminId,
            new CreateContentBatchRequest
            {
                CatalogName = "catalog.json",
                Items = [request]
            });

        Assert.Equal(ContentBatchItemStatus.Failed, batch.Items.Single().Status);
        Assert.Equal("invalid_path", batch.Items.Single().SafeFailureCategory);
    }

    [Fact]
    public async Task StartCreatesOneRevisionAndSkipsAnUnchangedProblem()
    {
        var fixture = new Fixture();
        fixture.Repository.Problems.Add(PublishedProblem("unchanged", Hash('a')));
        fixture.Repository.Problems.Add(PublishedProblem("already-exists", Hash('c')));
        var created = await fixture.Service.CreateAsync(
            fixture.AdminId,
            new CreateContentBatchRequest
            {
                CatalogName = "catalog.json",
                Items =
                [
                    ValidItem("new-problem", Hash('b')),
                    ValidItem(
                        "unchanged",
                        Hash('a'),
                        ContentBatchImportAction.NewRevision),
                    ValidItem("already-exists", Hash('d'))
                ]
            });

        var started = await fixture.Service.StartAsync(fixture.AdminId, created.Id);

        Assert.Equal(ContentBatchStatus.Generating, started.Status);
        Assert.Equal(ContentBatchItemStatus.Generating, started.Items[0].Status);
        Assert.Equal(ContentBatchItemStatus.Skipped, started.Items[1].Status);
        Assert.Equal(ContentBatchItemStatus.Failed, started.Items[2].Status);
        Assert.Equal("problem_exists", started.Items[2].SafeFailureCategory);
        Assert.Single(fixture.Repository.Jobs);
        Assert.Single(fixture.Repository.Problems.Single(
            problem => problem.Slug == "new-problem").AuthoringRevisions);
    }

    [Fact]
    public async Task RetryReusesFailedDraftRevisionWithoutCreatingADuplicate()
    {
        var fixture = new Fixture();
        var created = await fixture.Service.CreateAsync(
            fixture.AdminId,
            new CreateContentBatchRequest
            {
                CatalogName = "catalog.json",
                Items = [ValidItem("retry-me")]
            });
        await fixture.Service.StartAsync(fixture.AdminId, created.Id);
        var item = fixture.Repository.Batch!.Items.Single();
        var revision = item.Revision!;
        revision.GenerationJobs.Clear();
        revision.Status = AuthoringRevisionStatus.Draft;
        item.Status = ContentBatchItemStatus.Failed;
        item.SafeFailureCategory = "compile_error";

        var retried = await fixture.Service.RetryAsync(
            fixture.AdminId,
            created.Id,
            new RetryContentBatchRequest { ItemIds = [item.Id] });

        Assert.Equal(ContentBatchItemStatus.Retrying, retried.Items.Single().Status);
        Assert.Equal(revision.Id, retried.Items.Single().RevisionId);
        Assert.Single(fixture.Repository.Revisions);
        Assert.Equal(2, fixture.Repository.Jobs.Count);
    }

    [Fact]
    public async Task UpdateDraftReusesEditableRevisionAndReplacesItsSnapshot()
    {
        var fixture = new Fixture();
        var problem = PublishedProblem("editable", Hash('a'));
        problem.Status = ProblemStatus.Draft;
        var revision = problem.AuthoringRevisions.Single();
        revision.Status = AuthoringRevisionStatus.Draft;
        fixture.Repository.Problems.Add(problem);
        var created = await fixture.Service.CreateAsync(
            fixture.AdminId,
            new CreateContentBatchRequest
            {
                CatalogName = "catalog.json",
                Items =
                [
                    ValidItem(
                        "editable",
                        Hash('b'),
                        ContentBatchImportAction.UpdateDraft)
                ]
            });

        var started = await fixture.Service.StartAsync(fixture.AdminId, created.Id);

        Assert.Equal(revision.Id, started.Items.Single().RevisionId);
        Assert.Equal(Hash('b'), revision.ContentHash);
        Assert.Equal(AuthoringRevisionStatus.Generating, revision.Status);
        Assert.Empty(fixture.Repository.Revisions);
        Assert.Single(fixture.Repository.Jobs);
    }

    [Fact]
    public async Task NewRevisionLeavesPublishedRevisionImmutableAndPublishesOnlyApprovedItem()
    {
        var fixture = new Fixture();
        var existing = PublishedProblem("published", Hash('a'));
        fixture.Repository.Problems.Add(existing);
        var publishedRevision = existing.AuthoringRevisions.Single();
        var created = await fixture.Service.CreateAsync(
            fixture.AdminId,
            new CreateContentBatchRequest
            {
                CatalogName = "catalog.json",
                Items =
                [
                    ValidItem(
                        "published",
                        Hash('b'),
                        ContentBatchImportAction.NewRevision),
                    ValidItem("not-approved", Hash('c'))
                ]
            });
        await fixture.Service.StartAsync(fixture.AdminId, created.Id);
        var item = fixture.Repository.Batch!.Items.Single(value => value.Slug == "published");
        var unapproved = fixture.Repository.Batch.Items.Single(
            value => value.Slug == "not-approved");
        item.Status = ContentBatchItemStatus.Ready;
        item.Revision!.Status = AuthoringRevisionStatus.Ready;
        unapproved.Status = ContentBatchItemStatus.Ready;
        unapproved.Revision!.Status = AuthoringRevisionStatus.Ready;
        fixture.Repository.Batch.Status = ContentBatchStatus.ReadyForReview;

        var result = await fixture.Service.PublishAsync(
            fixture.AdminId,
            created.Id,
            new PublishContentBatchRequest { RevisionIds = [item.Revision.Id] });

        Assert.Equal(AuthoringRevisionStatus.Published, publishedRevision.Status);
        Assert.Equal(Hash('a'), publishedRevision.ContentHash);
        Assert.Equal(2, item.Revision.RevisionNumber);
        Assert.Equal(
            ContentBatchItemStatus.Published,
            result.Items.Single(value => value.Slug == "published").Status);
        Assert.Equal(
            ContentBatchItemStatus.Ready,
            result.Items.Single(value => value.Slug == "not-approved").Status);
        Assert.Equal(ContentBatchStatus.ReadyForReview, result.Status);
    }

    private static CreateContentBatchItemRequest ValidItem(
        string slug,
        string? hash = null,
        ContentBatchImportAction action = ContentBatchImportAction.Create) => new()
    {
        CatalogPath = $"problems/{slug}/problem.json",
        Action = action,
        ContentHash = hash ?? Hash('a'),
        Slug = slug,
        Title = $"Title {slug}",
        StatementMarkdown = "Statement",
        ConstraintsMarkdown = "Constraints",
        Difficulty = DifficultyLevel.Easy,
        Tags = ["arrays"],
        TimeLimitMs = 1_000,
        MemoryLimitKb = 262_144,
        Samples =
        [
            new ProblemSampleRequest
            {
                Input = "{\"values\":[1]}",
                ExpectedOutput = "1"
            }
        ],
        GeneratorParameters = JsonSerializer.SerializeToElement(new { count = 10 }),
        Definition = Definition()
    };

    private static CreateContentBatchItemRequest Copy(
        CreateContentBatchItemRequest source,
        string? validationFailureCategory,
        string? catalogPath = null) => new()
    {
        CatalogPath = catalogPath ?? source.CatalogPath,
        Action = source.Action,
        ContentHash = source.ContentHash,
        Slug = source.Slug,
        Title = source.Title,
        StatementMarkdown = source.StatementMarkdown,
        ConstraintsMarkdown = source.ConstraintsMarkdown,
        Difficulty = source.Difficulty,
        Tags = source.Tags,
        TimeLimitMs = source.TimeLimitMs,
        MemoryLimitKb = source.MemoryLimitKb,
        Samples = source.Samples,
        GeneratorParameters = source.GeneratorParameters,
        Definition = source.Definition,
        ValidationFailureCategory = validationFailureCategory
    };

    private static ProblemAuthoringDefinition Definition() => new()
    {
        SchemaVersion = 1,
        ExecutionMode = ProblemExecutionMode.Function,
        FunctionSignature = new FunctionSignature
        {
            ClassName = "Solution",
            MethodName = "solve",
            ReturnType = FunctionValueType.Int32,
            Parameters =
            [
                new FunctionParameter
                {
                    Name = "values",
                    Type = FunctionValueType.Int32Array
                }
            ]
        },
        HandwrittenCases =
        [
            new HandwrittenCaseDefinition
            {
                Name = "sample",
                Group = "handwritten",
                Arguments = JsonSerializer.SerializeToElement(new { values = new[] { 1 } })
            }
        ],
        Generator = new GeneratorSourceDefinition
        {
            Language = "csharp",
            SdkVersion = 1,
            Source = "generator source"
        },
        InputValidator = new GeneratorSourceDefinition
        {
            Language = "csharp",
            SdkVersion = 1,
            Source = "validator source"
        },
        ReferenceSolution = new FunctionSourceDefinition
        {
            Language = "cpp17",
            Source = "reference source"
        }
    };

    private static Problem PublishedProblem(string slug, string contentHash)
    {
        var problem = new Problem
        {
            Id = Random.Shared.Next(1, int.MaxValue),
            Slug = slug,
            Title = "Published",
            StatementMarkdown = "Published statement",
            ConstraintsMarkdown = "Published constraints",
            Status = ProblemStatus.Published,
            ExecutionMode = ProblemExecutionMode.Function,
            TimeLimitMs = 1_000,
            MemoryLimitKb = 262_144
        };
        problem.AuthoringRevisions.Add(new ProblemAuthoringRevision
        {
            Id = Guid.NewGuid(),
            Problem = problem,
            ProblemId = problem.Id,
            OwnerUserId = Guid.NewGuid(),
            RevisionNumber = 1,
            Status = AuthoringRevisionStatus.Published,
            Slug = slug,
            Title = problem.Title,
            StatementMarkdown = problem.StatementMarkdown,
            ConstraintsMarkdown = problem.ConstraintsMarkdown,
            TimeLimitMs = problem.TimeLimitMs,
            MemoryLimitKb = problem.MemoryLimitKb,
            DefinitionJson = "{}",
            DefinitionSha256 = Hash('d'),
            ContentHash = contentHash
        });
        return problem;
    }

    private static string Hash(char value) => new(value, 64);

    private sealed class Fixture
    {
        public Guid AdminId { get; } = Guid.NewGuid();
        public FakeRepository Repository { get; } = new();
        public ContentBatchService Service { get; }

        public Fixture()
        {
            Service = new ContentBatchService(
                Repository,
                new FakeAuthoringRepository(Repository),
                new FakeUnitOfWork());
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(1);
    }

    private sealed class FakeRepository : IContentBatchRepository
    {
        private int _nextProblemId = 100;
        public ContentBatch? Batch { get; private set; }
        public List<Problem> Problems { get; } = [];
        public List<ProblemAuthoringRevision> Revisions { get; } = [];
        public List<ContentGenerationJob> Jobs { get; } = [];

        public Task AddAsync(ContentBatch batch, CancellationToken cancellationToken = default)
        {
            Batch = batch;
            return Task.CompletedTask;
        }

        public Task<ContentBatch?> GetAsync(
            Guid batchId,
            bool includeAudit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Batch?.Id == batchId ? Batch : null);

        public Task<PagedResult<ContentBatch>> GetPagedAsync(
            ContentBatchStatus? status,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<ContentBatch>
            {
                Items = Batch is null ? [] : [Batch],
                TotalCount = Batch is null ? 0 : 1,
                PageNumber = pageNumber,
                PageSize = pageSize
            });

        public Task<Problem?> GetProblemBySlugAsync(
            string slug,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Problems.SingleOrDefault(problem => problem.Slug == slug));

        public Task<ProblemAuthoringRevision?> GetRevisionAsync(
            Guid revisionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Revisions.Concat(Problems.SelectMany(problem => problem.AuthoringRevisions))
                .SingleOrDefault(revision => revision.Id == revisionId));

        public Task AddProblemAsync(
            Problem problem,
            CancellationToken cancellationToken = default)
        {
            problem.Id = _nextProblemId++;
            Problems.Add(problem);
            return Task.CompletedTask;
        }

        public Task AddRevisionAsync(
            ProblemAuthoringRevision revision,
            CancellationToken cancellationToken = default)
        {
            revision.ProblemId = revision.Problem.Id;
            if (!revision.Problem.AuthoringRevisions.Contains(revision))
                revision.Problem.AuthoringRevisions.Add(revision);
            Revisions.Add(revision);
            return Task.CompletedTask;
        }

        public Task AddGenerationJobAsync(
            ContentGenerationJob job,
            CancellationToken cancellationToken = default)
        {
            job.Revision.GenerationJobs.Add(job);
            Jobs.Add(job);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAuthoringRepository(FakeRepository repository)
        : IProblemAuthoringRepository
    {
        public Task<bool> PublishAsync(
            Guid revisionId,
            Guid ownerUserId,
            CancellationToken cancellationToken = default)
        {
            var revision = repository.Revisions.Single(item => item.Id == revisionId);
            revision.Status = AuthoringRevisionStatus.Published;
            revision.Problem.Status = ProblemStatus.Published;
            return Task.FromResult(true);
        }

        public Task AddProblemAsync(Problem problem, CancellationToken cancellationToken = default) =>
            repository.AddProblemAsync(problem, cancellationToken);
        public Task AddRevisionAsync(ProblemAuthoringRevision revision, CancellationToken cancellationToken = default) =>
            repository.AddRevisionAsync(revision, cancellationToken);
        public Task<ProblemAuthoringRevision?> GetOwnedRevisionAsync(Guid revisionId, Guid ownerUserId, bool includeCandidate = false, CancellationToken cancellationToken = default) =>
            repository.GetRevisionAsync(revisionId, cancellationToken);
        public Task<ProblemAuthoringRevision?> GetLatestOwnedRevisionAsync(int problemId, Guid ownerUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult(repository.Problems.Single(problem => problem.Id == problemId)
                .AuthoringRevisions.OrderByDescending(revision => revision.RevisionNumber).FirstOrDefault());
        public Task<ProblemAuthoringRevision?> GetLatestRevisionAsync(int problemId, CancellationToken cancellationToken = default) =>
            GetLatestOwnedRevisionAsync(problemId, Guid.Empty, cancellationToken);
        public Task<ContentGenerationJob?> GetLatestJobAsync(Guid revisionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(repository.Jobs.LastOrDefault(job => job.RevisionId == revisionId));
        public Task AddGenerationJobAsync(ContentGenerationJob job, CancellationToken cancellationToken = default) =>
            repository.AddGenerationJobAsync(job, cancellationToken);
        public Task DeleteCandidateCasesAsync(Guid revisionId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
