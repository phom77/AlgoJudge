using System.Text.Json;
using AlgoJudge.Application.ContentGeneration;
using AlgoJudge.Application.Contracts.Admin;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Application.FunctionExecution;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;
using AlgoJudge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlgoJudge.Infrastructure.Repositories;

public sealed class ProblemAuthoringRepository : IProblemAuthoringRepository
{
    private readonly AppDbContext _context;

    public ProblemAuthoringRepository(AppDbContext context) => _context = context;

    public Task AddProblemAsync(Problem problem, CancellationToken cancellationToken = default) =>
        _context.Problems.AddAsync(problem, cancellationToken).AsTask();

    public Task AddRevisionAsync(ProblemAuthoringRevision revision, CancellationToken cancellationToken = default) =>
        _context.ProblemAuthoringRevisions.AddAsync(revision, cancellationToken).AsTask();

    public Task<ProblemAuthoringRevision?> GetOwnedRevisionAsync(
        Guid revisionId, Guid ownerUserId, bool includeCandidate = false,
        CancellationToken cancellationToken = default)
    {
        IQueryable<ProblemAuthoringRevision> query = _context.ProblemAuthoringRevisions
            .Include(item => item.Problem);
        if (includeCandidate)
            query = query.Include(item => item.CandidateTestCases);
        return query.SingleOrDefaultAsync(item => item.Id == revisionId && item.OwnerUserId == ownerUserId, cancellationToken);
    }

    public Task<ProblemAuthoringRevision?> GetLatestOwnedRevisionAsync(
        int problemId, Guid ownerUserId, CancellationToken cancellationToken = default) =>
        _context.ProblemAuthoringRevisions.Include(item => item.Problem)
            .Where(item => item.ProblemId == problemId && item.OwnerUserId == ownerUserId)
            .OrderByDescending(item => item.RevisionNumber).FirstOrDefaultAsync(cancellationToken);

    public Task<ContentGenerationJob?> GetLatestJobAsync(Guid revisionId, CancellationToken cancellationToken = default) =>
        _context.ContentGenerationJobs.AsNoTracking().Where(item => item.RevisionId == revisionId)
            .OrderByDescending(item => item.CreatedAt).FirstOrDefaultAsync(cancellationToken);

    public async Task DeleteCandidateCasesAsync(Guid revisionId, CancellationToken cancellationToken = default)
    {
        var candidates = await _context.AuthoringTestCases
            .Where(item => item.RevisionId == revisionId)
            .ToListAsync(cancellationToken);
        _context.AuthoringTestCases.RemoveRange(candidates);
    }

    public async Task<bool> PublishAsync(Guid revisionId, Guid ownerUserId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var revision = await _context.ProblemAuthoringRevisions
            .FromSqlInterpolated($"SELECT * FROM \"ProblemAuthoringRevisions\" WHERE \"Id\" = {revisionId} FOR UPDATE")
            .Include(item => item.Problem).Include(item => item.CandidateTestCases)
            .SingleOrDefaultAsync(cancellationToken);
        if (revision is null || revision.OwnerUserId != ownerUserId || revision.Status != AuthoringRevisionStatus.Ready ||
            revision.CandidateCaseCount is null || revision.CandidateTestCases.Count != revision.CandidateCaseCount.Value)
            return false;
        _ = await _context.Problems
            .FromSqlInterpolated($"SELECT * FROM \"Problems\" WHERE \"Id\" = {revision.ProblemId} FOR UPDATE")
            .SingleAsync(cancellationToken);

        var version = await _context.JudgeTestCases.Where(item => item.ProblemId == revision.ProblemId)
            .Select(item => (int?)item.SystemTestSuiteVersion).MaxAsync(cancellationToken) is { } current
            ? checked(current + 1) : 1;
        await _context.JudgeTestCases.AddRangeAsync(revision.CandidateTestCases.OrderBy(item => item.Ordinal).Select(item =>
            new JudgeTestCase
            {
                ProblemId = revision.ProblemId,
                SystemTestSuiteVersion = version,
                Ordinal = item.Ordinal,
                Input = item.Input,
                ExpectedOutput = item.ExpectedOutput
            }), cancellationToken);

        await _context.ProblemSamples.Where(item => item.ProblemId == revision.ProblemId).ExecuteDeleteAsync(cancellationToken);
        var samples = JsonSerializer.Deserialize<IReadOnlyList<ProblemSampleRequest>>(revision.SamplesJson, JsonOptions) ?? [];
        await _context.ProblemSamples.AddRangeAsync(samples.Select((sample, index) => new ProblemSample
        {
            ProblemId = revision.ProblemId,
            Ordinal = index + 1,
            Input = sample.Input,
            ExpectedOutput = sample.ExpectedOutput,
            Explanation = sample.Explanation
        }), cancellationToken);

        var definition = JsonSerializer.Deserialize<ProblemAuthoringDefinition>(revision.DefinitionJson, JsonOptions)
            ?? throw new InvalidOperationException("Stored authoring definition is invalid.");
        var problem = revision.Problem;
        problem.Slug = revision.Slug; problem.Title = revision.Title;
        problem.StatementMarkdown = revision.StatementMarkdown;
        problem.ConstraintsMarkdown = revision.ConstraintsMarkdown;
        problem.Difficulty = revision.Difficulty; problem.TimeLimitMs = revision.TimeLimitMs;
        problem.MemoryLimitKb = revision.MemoryLimitKb; problem.ExecutionMode = ProblemExecutionMode.Function;
        problem.FunctionSignatureJson = FunctionSignatureJsonSerializer.Serialize(definition.FunctionSignature);
        problem.FunctionAdapterTemplate = null; problem.JudgeVersion = version;
        problem.Status = ProblemStatus.Published; problem.PublishedAt = DateTime.UtcNow;
        problem.UpdatedAt = DateTime.UtcNow;
        revision.Status = AuthoringRevisionStatus.Published;
        revision.PublishedAt = DateTime.UtcNow; revision.UpdatedAt = DateTime.UtcNow;
        revision.ConcurrencyToken = Guid.NewGuid();
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(allowIntegerValues: false) }
    };
}
