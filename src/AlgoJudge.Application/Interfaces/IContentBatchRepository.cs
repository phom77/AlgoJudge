using AlgoJudge.Application.Models.Common;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Application.Interfaces;

public interface IContentBatchRepository
{
    Task AddAsync(ContentBatch batch, CancellationToken cancellationToken = default);
    Task<ContentBatch?> GetAsync(
        Guid batchId,
        bool includeAudit,
        CancellationToken cancellationToken = default);
    Task<PagedResult<ContentBatch>> GetPagedAsync(
        ContentBatchStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<Problem?> GetProblemBySlugAsync(
        string slug,
        CancellationToken cancellationToken = default);
    Task<ProblemAuthoringRevision?> GetRevisionAsync(
        Guid revisionId,
        CancellationToken cancellationToken = default);
    Task AddProblemAsync(Problem problem, CancellationToken cancellationToken = default);
    Task AddRevisionAsync(
        ProblemAuthoringRevision revision,
        CancellationToken cancellationToken = default);
    Task AddGenerationJobAsync(
        ContentGenerationJob job,
        CancellationToken cancellationToken = default);
}
