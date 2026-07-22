using AlgoJudge.Application.Contracts.Admin;
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
        public Task DeleteCandidateCasesAsync(Guid revisionId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> PublishAsync(Guid revisionId, Guid ownerUserId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}
