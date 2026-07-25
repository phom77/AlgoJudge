namespace AlgoJudge.Application.ContentGeneration;

public sealed class SuiteQualityPolicy
{
    private static readonly HashSet<string> AllowedGroups =
        ["handwritten", "edge", "random", "adversarial", "stress"];

    public int MinimumTestCaseCount { get; init; } = 1;
    public IReadOnlyList<SuiteQualityGroupRequirement> MinimumCasesByGroup { get; init; } =
        [new SuiteQualityGroupRequirement { Group = "handwritten", MinimumCaseCount = 1 }];
    public bool RequireEachDeclaredWrongSolutionKilled { get; init; } = true;

    public void Validate()
    {
        if (MinimumTestCaseCount is < 1 or > 5_000 ||
            MinimumCasesByGroup is null || MinimumCasesByGroup.Count > AllowedGroups.Count ||
            MinimumCasesByGroup.Any(item => item is null ||
                !AllowedGroups.Contains(item.Group) ||
                item.MinimumCaseCount is < 1 or > 5_000) ||
            MinimumCasesByGroup.Select(item => item.Group).Distinct(StringComparer.Ordinal).Count() !=
            MinimumCasesByGroup.Count)
        {
            throw new ArgumentException("The suite quality policy is invalid.");
        }
    }
}
