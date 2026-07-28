using AlgoJudge.Application.Contracts.Admin;
using AlgoJudge.Application.Contracts.Common;

namespace AlgoJudge.Application.Interfaces;

public interface IContentBatchService
{
    Task<ContentBatchResponse> CreateAsync(
        Guid adminUserId,
        CreateContentBatchRequest request,
        CancellationToken cancellationToken = default);
    Task<PagedResponse<ContentBatchListItemResponse>> GetBatchesAsync(
        ContentBatchListQuery query,
        CancellationToken cancellationToken = default);
    Task<ContentBatchResponse> GetAsync(
        Guid batchId,
        CancellationToken cancellationToken = default);
    Task<ContentBatchResponse> StartAsync(
        Guid adminUserId,
        Guid batchId,
        CancellationToken cancellationToken = default);
    Task<ContentBatchResponse> ResumeAsync(
        Guid adminUserId,
        Guid batchId,
        CancellationToken cancellationToken = default);
    Task<ContentBatchResponse> RetryAsync(
        Guid adminUserId,
        Guid batchId,
        RetryContentBatchRequest request,
        CancellationToken cancellationToken = default);
    Task<ContentBatchResponse> PublishAsync(
        Guid adminUserId,
        Guid batchId,
        PublishContentBatchRequest request,
        CancellationToken cancellationToken = default);
}
