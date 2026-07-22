using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Domain.Entities;

public sealed class ContentGenerationJob
{
    public Guid Id { get; set; }
    public Guid RevisionId { get; set; }
    public ContentGenerationJobStatus Status { get; set; } = ContentGenerationJobStatus.Pending;
    public string DefinitionSnapshotJson { get; set; } = "{}";
    public string DefinitionSha256 { get; set; } = string.Empty;
    public int TimeLimitMs { get; set; }
    public int MemoryLimitKb { get; set; }
    public int AttemptCount { get; set; }
    public string? WorkerId { get; set; }
    public Guid? ClaimToken { get; set; }
    public DateTime? LeaseExpiresAt { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public ProblemAuthoringRevision Revision { get; set; } = null!;
}
