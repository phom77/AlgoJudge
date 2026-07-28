using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Domain.Entities;

public sealed class ContentBatch
{
    public Guid Id { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string CatalogName { get; set; } = string.Empty;
    public ContentBatchStatus Status { get; set; } = ContentBatchStatus.Created;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
    public User CreatedByUser { get; set; } = null!;
    public ICollection<ContentBatchItem> Items { get; set; } = [];
    public ICollection<ContentBatchAuditEntry> AuditEntries { get; set; } = [];
}
