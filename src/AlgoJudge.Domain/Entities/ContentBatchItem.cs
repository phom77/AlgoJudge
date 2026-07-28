using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Domain.Entities;

public sealed class ContentBatchItem
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public int Ordinal { get; set; }
    public string CatalogPath { get; set; } = string.Empty;
    public ContentBatchImportAction Action { get; set; }
    public ContentBatchItemStatus Status { get; set; } = ContentBatchItemStatus.Pending;
    public string? ContentHash { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string StatementMarkdown { get; set; } = string.Empty;
    public string ConstraintsMarkdown { get; set; } = string.Empty;
    public DifficultyLevel Difficulty { get; set; }
    public int TimeLimitMs { get; set; }
    public int MemoryLimitKb { get; set; }
    public string TagsJson { get; set; } = "[]";
    public string SamplesJson { get; set; } = "[]";
    public string DefinitionJson { get; set; } = "{}";
    public string GeneratorParametersJson { get; set; } = "{}";
    public int? ProblemId { get; set; }
    public Guid? RevisionId { get; set; }
    public string? SafeFailureCategory { get; set; }
    public string? SafeFailureMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }
    public ContentBatch Batch { get; set; } = null!;
    public Problem? Problem { get; set; }
    public ProblemAuthoringRevision? Revision { get; set; }
    public ICollection<ContentGenerationJob> GenerationJobs { get; set; } = [];
    public ICollection<ContentBatchAuditEntry> AuditEntries { get; set; } = [];
}
