using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Application.Contracts.Admin;

public sealed class ContentBatchItemResponse
{
    public Guid Id { get; init; }
    public int Ordinal { get; init; }
    public string CatalogPath { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public ContentBatchImportAction Action { get; init; }
    public ContentBatchItemStatus Status { get; init; }
    public string? ContentHash { get; init; }
    public int? ProblemId { get; init; }
    public Guid? RevisionId { get; init; }
    public string? SafeFailureCategory { get; init; }
    public string? SafeFailureMessage { get; init; }
    public DateTime UpdatedAt { get; init; }
}
