using AlgoJudge.Application.Contracts.Admin;
using AlgoJudge.Application.Contracts.Common;
using AlgoJudge.Application.Exceptions;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Application.Models.Admin;
using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Application.Services;

public sealed class ProblemManagementService : IProblemManagementService
{
    private readonly IProblemManagementRepository repository;

    public ProblemManagementService(IProblemManagementRepository repository) =>
        this.repository = repository;

    public async Task<PagedResponse<AdminProblemListItemResponse>> GetProblemsAsync(
        AdminProblemListQuery query,
        CancellationToken cancellationToken = default)
    {
        ValidateQuery(query);
        var page = await repository.GetPagedAsync(
            query.Search?.Trim(),
            query.Status,
            query.PageNumber,
            query.PageSize,
            cancellationToken);
        return new PagedResponse<AdminProblemListItemResponse>
        {
            Items = page.Items.Select(MapListItem).ToArray(),
            TotalCount = page.TotalCount,
            PageNumber = page.PageNumber,
            PageSize = page.PageSize
        };
    }

    public async Task<AdminProblemResponse> GetProblemAsync(
        int problemId,
        CancellationToken cancellationToken = default) =>
        Map(await GetRequiredAsync(problemId, cancellationToken));

    public Task ArchiveAsync(int problemId, CancellationToken cancellationToken = default) =>
        TransitionAsync(problemId, ProblemStatus.Published, ProblemStatus.Archived, cancellationToken);

    public Task RestoreAsync(int problemId, CancellationToken cancellationToken = default) =>
        TransitionAsync(problemId, ProblemStatus.Archived, ProblemStatus.Published, cancellationToken);

    private async Task TransitionAsync(
        int problemId,
        ProblemStatus expectedStatus,
        ProblemStatus nextStatus,
        CancellationToken cancellationToken)
    {
        if (problemId <= 0)
            throw new RequestValidationException("Problem ID must be positive.");
        if (await repository.TransitionStatusAsync(
                problemId, expectedStatus, nextStatus, cancellationToken))
            return;

        var problem = await repository.GetAsync(problemId, cancellationToken)
            ?? throw new ResourceNotFoundException("Problem was not found.");
        throw new ConflictException($"Problem cannot transition from {problem.Status} to {nextStatus}.");
    }

    private async Task<AdminProblemDto> GetRequiredAsync(int problemId, CancellationToken cancellationToken)
    {
        if (problemId <= 0)
            throw new RequestValidationException("Problem ID must be positive.");
        return await repository.GetAsync(problemId, cancellationToken)
            ?? throw new ResourceNotFoundException("Problem was not found.");
    }

    private static void ValidateQuery(AdminProblemListQuery query)
    {
        if (query.PageNumber < 1)
            throw new RequestValidationException("Page number must be at least 1.");
        if (query.PageSize is < 1 or > 100)
            throw new RequestValidationException("Page size must be between 1 and 100.");
        if (query.Search?.Length > 100)
            throw new RequestValidationException("Search must not exceed 100 characters.");
        if (query.Status.HasValue && !Enum.IsDefined(query.Status.Value))
            throw new RequestValidationException("Problem status is invalid.");
    }

    private static AdminProblemListItemResponse MapListItem(AdminProblemDto problem) => new()
    {
        Id = problem.Id,
        Slug = problem.Slug,
        Title = problem.Title,
        Difficulty = problem.Difficulty,
        Status = problem.Status,
        JudgeVersion = problem.JudgeVersion,
        LatestRevisionId = problem.LatestRevisionId,
        LatestRevisionStatus = problem.LatestRevisionStatus,
        PublishedAt = problem.PublishedAt,
        UpdatedAt = problem.UpdatedAt
    };

    private static AdminProblemResponse Map(AdminProblemDto problem) => new()
    {
        Id = problem.Id,
        Slug = problem.Slug,
        Title = problem.Title,
        StatementMarkdown = problem.StatementMarkdown,
        ConstraintsMarkdown = problem.ConstraintsMarkdown,
        Difficulty = problem.Difficulty,
        ExecutionMode = problem.ExecutionMode,
        TimeLimitMs = problem.TimeLimitMs,
        MemoryLimitKb = problem.MemoryLimitKb,
        Status = problem.Status,
        JudgeVersion = problem.JudgeVersion,
        LatestRevisionId = problem.LatestRevisionId,
        LatestRevisionStatus = problem.LatestRevisionStatus,
        PublishedAt = problem.PublishedAt,
        CreatedAt = problem.CreatedAt,
        UpdatedAt = problem.UpdatedAt
    };
}
