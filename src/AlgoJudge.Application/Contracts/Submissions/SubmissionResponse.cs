using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Application.Contracts.Submissions;

public sealed class SubmissionResponse
{
    public Guid Id { get; init; }
    public int ProblemId { get; init; }
    public string Language { get; init; } = string.Empty;
    public SubmissionStatus Status { get; init; }
    public int ExecutionTimeMs { get; init; }
    public int MemoryUsedKb { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? FinishedAt { get; init; }
}
