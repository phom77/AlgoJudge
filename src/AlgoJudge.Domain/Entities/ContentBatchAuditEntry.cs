namespace AlgoJudge.Domain.Entities;

public sealed class ContentBatchAuditEntry
{
    public long Id { get; set; }
    public Guid BatchId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid AdminUserId { get; set; }
    public int? ProblemId { get; set; }
    public Guid? RevisionId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string? SafeFailureCategory { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ContentBatch Batch { get; set; } = null!;
    public ContentBatchItem? Item { get; set; }
    public User AdminUser { get; set; } = null!;
}
