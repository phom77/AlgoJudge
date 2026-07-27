using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Application.Models.Admin;

public sealed class AdminProblemDto
{
    public int Id { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string StatementMarkdown { get; init; } = string.Empty;
    public string ConstraintsMarkdown { get; init; } = string.Empty;
    public DifficultyLevel Difficulty { get; init; }
    public ProblemExecutionMode ExecutionMode { get; init; }
    public int TimeLimitMs { get; init; }
    public int MemoryLimitKb { get; init; }
    public ProblemStatus Status { get; init; }
    public int JudgeVersion { get; init; }
    public Guid? LatestRevisionId { get; init; }
    public AuthoringRevisionStatus? LatestRevisionStatus { get; init; }
    public DateTime? PublishedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
