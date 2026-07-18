using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Application.Contracts.Runs;

public sealed class RunResponse
{
    public Guid Id { get; init; }
    public int ProblemId { get; init; }
    public RunStatus Status { get; init; }
    public string? Stdout { get; init; }
    public string? Stderr { get; init; }
    public int ExecutionTimeMs { get; init; }
    public int MemoryUsedKb { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? FinishedAt { get; init; }
}
