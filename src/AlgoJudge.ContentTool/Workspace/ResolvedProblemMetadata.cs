using AlgoJudge.Domain.Enums;
using AlgoJudge.Domain.Execution;

namespace AlgoJudge.ContentTool.Workspace;

public sealed class ResolvedProblemMetadata
{
    public string Slug { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public DifficultyLevel Difficulty { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public string Statement { get; init; } = string.Empty;
    public IReadOnlyList<string> Constraints { get; init; } = [];
    public int TimeLimitMs { get; init; }
    public int MemoryLimitKb { get; init; }
    public string Language { get; init; } = string.Empty;
    public OutputCheckerConfiguration OutputChecker { get; init; } =
        OutputCheckerConfiguration.JsonExact;
    public IReadOnlyList<ContentProblemSample> Samples { get; init; } = [];
}
