using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Application.Contracts.Admin;

public sealed class CreateProblemDraftRequest
{
    public string Slug { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string StatementMarkdown { get; init; } = string.Empty;
    public string ConstraintsMarkdown { get; init; } = string.Empty;
    public DifficultyLevel Difficulty { get; init; }
    public int TimeLimitMs { get; init; }
    public int MemoryLimitKb { get; init; }
    public IReadOnlyList<ProblemSampleRequest> Samples { get; init; } = [];
}
