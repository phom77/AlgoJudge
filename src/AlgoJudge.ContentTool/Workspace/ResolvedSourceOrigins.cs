namespace AlgoJudge.ContentTool.Workspace;

public sealed class ResolvedSourceOrigins
{
    public string Generator { get; init; } = string.Empty;
    public string InputValidator { get; init; } = string.Empty;
    public string ReferenceSolution { get; init; } = string.Empty;
    public IReadOnlyList<string> WrongSolutions { get; init; } = [];
}
