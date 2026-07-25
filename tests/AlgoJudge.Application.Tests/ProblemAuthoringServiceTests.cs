using AlgoJudge.Application.Contracts.Admin;
using AlgoJudge.Application.ContentGeneration;
using AlgoJudge.Application.Exceptions;
using AlgoJudge.Application.FunctionExecution;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Application.Services;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Application.Tests;

public sealed class ProblemAuthoringServiceTests
{
    [Fact]
    public async Task DraftEditsCreateImmutableGenerationSnapshotAndLockRevision()
    {
        var repository = new FakeRepository();
        var service = new ProblemAuthoringService(repository, new FakeUnitOfWork());
        var owner = Guid.NewGuid();
        var draft = await service.CreateDraftAsync(owner, ValidCreateRequest());
        await service.UpdateSignatureAsync(owner, draft.RevisionId, new UpdateFunctionSignatureRequest
        {
            Signature = new FunctionSignature
            {
                ClassName = "Solution",
                MethodName = "solve",
                ReturnType = FunctionValueType.Int32,
                Parameters = [new FunctionParameter { Name = "values", Type = FunctionValueType.Int32Array }]
            }
        });
        await service.UpdateHandwrittenCasesAsync(owner, draft.RevisionId, new UpdateHandwrittenCasesRequest
        {
            Cases = [new() { Name = "single", Arguments = System.Text.Json.JsonSerializer.SerializeToElement(new { values = new[] { 1 } }) }]
        });
        await service.UpdateSourcesAsync(owner, draft.RevisionId, new UpdateAuthoringSourcesRequest
        {
            Generator = new() { Language = "csharp", SdkVersion = 1, Source = "generator" },
            InputValidator = new() { Language = "csharp", SdkVersion = 1, Source = "validator" },
            ReferenceSolution = new() { Language = "cpp17", Source = "solution" }
        });

        var job = await service.StartGenerationAsync(owner, draft.RevisionId);

        Assert.Equal(ContentGenerationJobStatus.Pending, job.JobStatus);
        Assert.Equal(AuthoringRevisionStatus.Generating, job.RevisionStatus);
        Assert.Equal(repository.Revision!.DefinitionJson, repository.Job!.DefinitionSnapshotJson);
        Assert.Equal(repository.Revision.DefinitionSha256, repository.Job.DefinitionSha256);
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.UpdateSignatureAsync(owner, draft.RevisionId, new UpdateFunctionSignatureRequest()));
    }

    [Fact]
    public async Task AnotherMaintainerCannotReadOwnedRevision()
    {
        var repository = new FakeRepository();
        var service = new ProblemAuthoringService(repository, new FakeUnitOfWork());
        var draft = await service.CreateDraftAsync(Guid.NewGuid(), ValidCreateRequest());

        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            service.GetDraftAsync(Guid.NewGuid(), draft.RevisionId));
    }

    [Fact]
    public async Task SuiteReviewExposesOnlyBoundedCandidateMetadata()
    {
        var repository = new FakeRepository();
        var service = new ProblemAuthoringService(repository, new FakeUnitOfWork());
        var owner = Guid.NewGuid();
        var draft = await service.CreateDraftAsync(owner, ValidCreateRequest());
        repository.Revision!.Status = AuthoringRevisionStatus.Ready;
        repository.Revision.CandidateSuiteSha256 = new string('a', 64);
        repository.Revision.CandidateToolchain = "generator-sdk-v1";
        repository.Revision.CandidateCaseCount = 101;
        repository.Revision.CandidateStatisticsJson =
            "{\"casesByGroup\":{\"random\":101},\"wrongSolutionCount\":1," +
            "\"killedCaseCountByWrongSolution\":{\"off-by-one\":100}," +
            "\"survivingWrongSolutions\":[]}";
        for (var index = 1; index <= 101; index++)
        {
            repository.Revision.CandidateTestCases.Add(new AuthoringTestCase
            {
                Ordinal = index,
                Name = $"random-{index}",
                Group = "random",
                Seed = index * 17,
                Input = "private input",
                ExpectedOutput = "private output",
                KilledWrongSolutionsJson = "[\"off-by-one\"]"
            });
        }

        var review = await service.GetSuiteReviewAsync(owner, draft.RevisionId);

        Assert.Equal(100, review.CasePreview.Count);
        Assert.True(review.IsCasePreviewTruncated);
        Assert.Equal(17, review.CasePreview[0].Seed);
        Assert.Equal(["off-by-one"], review.CasePreview[0].KilledWrongSolutions);
        Assert.DoesNotContain(review.GetType().GetProperties(), property =>
            property.Name.Contains("Input", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("ExpectedOutput", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UpdatingQualityPolicyInvalidatesReadyCandidateAndSnapshotsThePolicy()
    {
        var repository = new FakeRepository();
        var service = new ProblemAuthoringService(repository, new FakeUnitOfWork());
        var owner = Guid.NewGuid();
        var draft = await service.CreateDraftAsync(owner, ValidCreateRequest());
        repository.Revision!.Status = AuthoringRevisionStatus.Ready;
        repository.Revision.CandidateSuiteSha256 = new string('a', 64);
        repository.Revision.CandidateCaseCount = 1;

        var updated = await service.UpdateQualityPolicyAsync(owner, draft.RevisionId,
            new UpdateSuiteQualityPolicyRequest
            {
                QualityPolicy = new SuiteQualityPolicy
                {
                    MinimumTestCaseCount = 500,
                    MinimumCasesByGroup =
                    [
                        new SuiteQualityGroupRequirement
                        {
                            Group = "random",
                            MinimumCaseCount = 400
                        }
                    ]
                }
            });

        Assert.Equal(AuthoringRevisionStatus.Draft, updated.Status);
        Assert.Equal(500, updated.Definition.QualityPolicy.MinimumTestCaseCount);
        Assert.Equal("random", updated.Definition.QualityPolicy.MinimumCasesByGroup[0].Group);
        Assert.Null(repository.Revision.CandidateSuiteSha256);
        Assert.Null(repository.Revision.CandidateCaseCount);
    }

    [Fact]
    public async Task UpdatingQualityPolicyRejectsNullPolicy()
    {
        var repository = new FakeRepository();
        var service = new ProblemAuthoringService(repository, new FakeUnitOfWork());
        var owner = Guid.NewGuid();
        var draft = await service.CreateDraftAsync(owner, ValidCreateRequest());

        await Assert.ThrowsAsync<RequestValidationException>(() =>
            service.UpdateQualityPolicyAsync(owner, draft.RevisionId,
                new UpdateSuiteQualityPolicyRequest { QualityPolicy = null! }));
    }

    private static CreateProblemDraftRequest ValidCreateRequest() => new()
    {
        Slug = "maximum-array",
        Title = "Maximum Array",
        StatementMarkdown = "Statement",
        ConstraintsMarkdown = "Constraints",
        Difficulty = DifficultyLevel.Easy,
        TimeLimitMs = 1000,
        MemoryLimitKb = 262144,
        Samples = [new ProblemSampleRequest { Input = "{\"values\":[1]}", ExpectedOutput = "1" }]
    };

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class FakeRepository : IProblemAuthoringRepository
    {
        public ProblemAuthoringRevision? Revision { get; private set; }
        public ContentGenerationJob? Job => Revision?.GenerationJobs.LastOrDefault();
        public Task AddProblemAsync(Problem problem, CancellationToken cancellationToken = default) { problem.Id = 42; return Task.CompletedTask; }
        public Task AddRevisionAsync(ProblemAuthoringRevision revision, CancellationToken cancellationToken = default)
        { revision.ProblemId = revision.Problem.Id; Revision = revision; return Task.CompletedTask; }
        public Task<ProblemAuthoringRevision?> GetOwnedRevisionAsync(Guid revisionId, Guid ownerUserId, bool includeCandidate = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(Revision is not null && Revision.Id == revisionId && Revision.OwnerUserId == ownerUserId ? Revision : null);
        public Task<ProblemAuthoringRevision?> GetLatestOwnedRevisionAsync(int problemId, Guid ownerUserId, CancellationToken cancellationToken = default) => Task.FromResult<ProblemAuthoringRevision?>(Revision);
        public Task<ContentGenerationJob?> GetLatestJobAsync(Guid revisionId, CancellationToken cancellationToken = default)
        { return Task.FromResult(Job); }
        public Task AddGenerationJobAsync(ContentGenerationJob job, CancellationToken cancellationToken = default)
        {
            Revision!.GenerationJobs.Add(job);
            return Task.CompletedTask;
        }
        public Task DeleteCandidateCasesAsync(Guid revisionId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> PublishAsync(Guid revisionId, Guid ownerUserId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}
