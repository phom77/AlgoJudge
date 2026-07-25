namespace AlgoJudge.Application.ContentGeneration;

public sealed class SuiteQualityGroupRequirement
{
    public string Group { get; init; } = string.Empty;
    public int MinimumCaseCount { get; init; }
}
