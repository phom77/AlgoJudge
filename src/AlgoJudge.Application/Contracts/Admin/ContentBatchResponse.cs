using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Application.Contracts.Admin;

public sealed class ContentBatchResponse
{
    public Guid Id { get; init; }
    public string CatalogName { get; init; } = string.Empty;
    public ContentBatchStatus Status { get; init; }
    public Guid CreatedByUserId { get; init; }
    public ContentBatchCountsResponse Counts { get; init; } = new();
    public IReadOnlyList<ContentBatchItemResponse> Items { get; init; } = [];
    public IReadOnlyList<ContentBatchAuditResponse> AuditEntries { get; init; } = [];
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}
