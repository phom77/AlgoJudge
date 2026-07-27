using AlgoJudge.Application.Interfaces;
using AlgoJudge.Application.Models.Admin;
using AlgoJudge.Application.Models.Common;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;
using AlgoJudge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace AlgoJudge.Infrastructure.Repositories;

public sealed class ProblemManagementRepository : IProblemManagementRepository
{
    private readonly AppDbContext context;

    public ProblemManagementRepository(AppDbContext context) => this.context = context;

    public async Task<PagedResult<AdminProblemDto>> GetPagedAsync(
        string? search,
        ProblemStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(search, status);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(problem => problem.UpdatedAt)
            .ThenByDescending(problem => problem.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(ListProject())
            .ToListAsync(cancellationToken);
        return new PagedResult<AdminProblemDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public Task<AdminProblemDto?> GetAsync(int problemId, CancellationToken cancellationToken = default) =>
        context.Problems.AsNoTracking()
            .Where(problem => problem.Id == problemId)
            .Select(DetailProject())
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<bool> TransitionStatusAsync(
        int problemId,
        ProblemStatus expectedStatus,
        ProblemStatus nextStatus,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var updated = await context.Problems
            .Where(problem => problem.Id == problemId && problem.Status == expectedStatus)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(problem => problem.Status, nextStatus)
                .SetProperty(problem => problem.UpdatedAt, now), cancellationToken);
        return updated == 1;
    }

    private IQueryable<Problem> BuildQuery(string? search, ProblemStatus? status)
    {
        var query = context.Problems.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var escapedSearch = search
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal)
                .Replace("_", "\\_", StringComparison.Ordinal);
            var pattern = $"%{escapedSearch}%";
            query = query.Where(problem =>
                EF.Functions.ILike(problem.Title, pattern, "\\") ||
                EF.Functions.ILike(problem.Slug, pattern, "\\"));
        }

        return status.HasValue
            ? query.Where(problem => problem.Status == status.Value)
            : query;
    }

    private static Expression<Func<Problem, AdminProblemDto>> ListProject() => problem => new()
    {
        Id = problem.Id,
        Slug = problem.Slug,
        Title = problem.Title,
        Difficulty = problem.Difficulty,
        Status = problem.Status,
        JudgeVersion = problem.JudgeVersion,
        LatestRevisionId = problem.AuthoringRevisions
            .OrderByDescending(revision => revision.RevisionNumber)
            .Select(revision => (Guid?)revision.Id)
            .FirstOrDefault(),
        LatestRevisionStatus = problem.AuthoringRevisions
            .OrderByDescending(revision => revision.RevisionNumber)
            .Select(revision => (AuthoringRevisionStatus?)revision.Status)
            .FirstOrDefault(),
        PublishedAt = problem.PublishedAt,
        UpdatedAt = problem.UpdatedAt
    };

    private static Expression<Func<Problem, AdminProblemDto>> DetailProject() => problem => new()
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
        LatestRevisionId = problem.AuthoringRevisions
            .OrderByDescending(revision => revision.RevisionNumber)
            .Select(revision => (Guid?)revision.Id)
            .FirstOrDefault(),
        LatestRevisionStatus = problem.AuthoringRevisions
            .OrderByDescending(revision => revision.RevisionNumber)
            .Select(revision => (AuthoringRevisionStatus?)revision.Status)
            .FirstOrDefault(),
        PublishedAt = problem.PublishedAt,
        CreatedAt = problem.CreatedAt,
        UpdatedAt = problem.UpdatedAt
    };
}
