namespace AlgoJudge.Application.Contracts.Admin;

public sealed class ContentBatchAuditResponse
{
    public long Id { get; init; }
    public Guid AdminUserId { get; init; }
    public Guid? ItemId { get; init; }
    public int? ProblemId { get; init; }
    public Guid? RevisionId { get; init; }
    public string Action { get; init; } = string.Empty;
    public string Result { get; init; } = string.Empty;
    public string? SafeFailureCategory { get; init; }
    public DateTime CreatedAt { get; init; }
}
