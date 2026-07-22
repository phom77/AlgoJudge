using AlgoJudge.Application.ContentGeneration;
using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Application.Contracts.Admin;

public sealed class ProblemDraftResponse
{
    public Guid RevisionId { get; init; }
    public int ProblemId { get; init; }
    public int RevisionNumber { get; init; }
    public AuthoringRevisionStatus Status { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string StatementMarkdown { get; init; } = string.Empty;
    public string ConstraintsMarkdown { get; init; } = string.Empty;
    public DifficultyLevel Difficulty { get; init; }
    public int TimeLimitMs { get; init; }
    public int MemoryLimitKb { get; init; }
    public IReadOnlyList<ProblemSampleRequest> Samples { get; init; } = [];
    public ProblemAuthoringDefinition Definition { get; init; } = new();
    public DateTime UpdatedAt { get; init; }
}
