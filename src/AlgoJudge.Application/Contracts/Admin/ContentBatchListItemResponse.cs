using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Application.Contracts.Admin;

public sealed class ContentBatchListItemResponse
{
    public Guid Id { get; init; }
    public string CatalogName { get; init; } = string.Empty;
    public ContentBatchStatus Status { get; init; }
    public Guid CreatedByUserId { get; init; }
    public ContentBatchCountsResponse Counts { get; init; } = new();
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
