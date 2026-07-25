namespace AlgoJudge.Application.ContentGeneration;

public static class SuiteQualityGate
{
    public static IReadOnlyList<string> Evaluate(
        SuiteQualityPolicy policy,
        int testCaseCount,
        IReadOnlyDictionary<string, int> casesByGroup,
        IReadOnlyCollection<string> declaredWrongSolutions,
        IReadOnlyCollection<string> survivingWrongSolutions)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(casesByGroup);
        ArgumentNullException.ThrowIfNull(declaredWrongSolutions);
        ArgumentNullException.ThrowIfNull(survivingWrongSolutions);
        policy.Validate();

        var violations = new List<string>();
        if (testCaseCount < policy.MinimumTestCaseCount)
            violations.Add("minimum_case_count");

        foreach (var requirement in policy.MinimumCasesByGroup)
        {
            if (!casesByGroup.TryGetValue(requirement.Group, out var count) ||
                count < requirement.MinimumCaseCount)
            {
                violations.Add($"minimum_group_count:{requirement.Group}");
            }
        }

        if (policy.RequireEachDeclaredWrongSolutionKilled &&
            declaredWrongSolutions.Count > 0 && survivingWrongSolutions.Count > 0)
        {
            violations.Add("surviving_wrong_solution");
        }

        return violations;
    }
}
