using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Domain.Entities;

public sealed class CodeRun
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int ProblemId { get; set; }
    public string SourceCode { get; set; } = string.Empty;
    public string Language { get; set; } = "cpp17";
    public string Input { get; set; } = string.Empty;
    public RunStatus Status { get; set; } = RunStatus.Pending;
    public string? StandardOutput { get; set; }
    public string? ErrorOutput { get; set; }
    public int ExecutionTimeMs { get; set; }
    public int MemoryUsedKb { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public DateTime? LeaseExpiresAt { get; set; }
    public string? WorkerId { get; set; }
    public Guid? ClaimToken { get; set; }
    public int AttemptCount { get; set; }
    public User User { get; set; } = null!;
    public Problem Problem { get; set; } = null!;
}
