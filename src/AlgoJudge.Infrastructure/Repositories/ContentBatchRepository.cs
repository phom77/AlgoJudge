using AlgoJudge.Application.Interfaces;
using AlgoJudge.Application.Models.Common;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;
using AlgoJudge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlgoJudge.Infrastructure.Repositories;

public sealed class ContentBatchRepository : IContentBatchRepository
{
    private readonly AppDbContext _context;

    public ContentBatchRepository(AppDbContext context) => _context = context;

    public Task AddAsync(ContentBatch batch, CancellationToken cancellationToken = default) =>
        _context.ContentBatches.AddAsync(batch, cancellationToken).AsTask();

    public Task<ContentBatch?> GetAsync(
        Guid batchId,
        bool includeAudit,
        CancellationToken cancellationToken = default)
    {
        IQueryable<ContentBatch> query = _context.ContentBatches
            .Include(batch => batch.Items)
                .ThenInclude(item => item.Revision)
            .Include(batch => batch.Items)
                .ThenInclude(item => item.GenerationJobs);
        if (includeAudit)
            query = query.Include(batch => batch.AuditEntries);
        return query.SingleOrDefaultAsync(batch => batch.Id == batchId, cancellationToken);
    }

    public async Task<PagedResult<ContentBatch>> GetPagedAsync(
        ContentBatchStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        IQueryable<ContentBatch> query = _context.ContentBatches
            .AsNoTracking()
            .Include(batch => batch.Items);
        if (status.HasValue)
            query = query.Where(batch => batch.Status == status.Value);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(batch => batch.CreatedAt)
            .ThenByDescending(batch => batch.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<ContentBatch>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public Task<Problem?> GetProblemBySlugAsync(
        string slug,
        CancellationToken cancellationToken = default) =>
        _context.Problems
            .Include(problem => problem.AuthoringRevisions)
            .SingleOrDefaultAsync(problem => problem.Slug == slug, cancellationToken);

    public Task<ProblemAuthoringRevision?> GetRevisionAsync(
        Guid revisionId,
        CancellationToken cancellationToken = default) =>
        _context.ProblemAuthoringRevisions
            .Include(revision => revision.GenerationJobs)
            .SingleOrDefaultAsync(revision => revision.Id == revisionId, cancellationToken);

    public Task AddProblemAsync(
        Problem problem,
        CancellationToken cancellationToken = default) =>
        _context.Problems.AddAsync(problem, cancellationToken).AsTask();

    public Task AddRevisionAsync(
        ProblemAuthoringRevision revision,
        CancellationToken cancellationToken = default) =>
        _context.ProblemAuthoringRevisions.AddAsync(revision, cancellationToken).AsTask();

    public Task AddGenerationJobAsync(
        ContentGenerationJob job,
        CancellationToken cancellationToken = default) =>
        _context.ContentGenerationJobs.AddAsync(job, cancellationToken).AsTask();
}
