using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Application.Contracts.Admin;

public sealed class AdminProblemListItemResponse
{
    public int Id { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public DifficultyLevel Difficulty { get; init; }
    public ProblemStatus Status { get; init; }
    public int JudgeVersion { get; init; }
    public Guid? LatestRevisionId { get; init; }
    public AuthoringRevisionStatus? LatestRevisionStatus { get; init; }
    public DateTime? PublishedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
