namespace AlgoJudge.Application.Contracts.Admin;

public sealed class GeneratedSuiteReviewResponse
{
    public Guid RevisionId { get; init; }
    public string SuiteSha256 { get; init; } = string.Empty;
    public int TestCaseCount { get; init; }
    public IReadOnlyDictionary<string, int> CasesByGroup { get; init; } =
        new Dictionary<string, int>();
    public int WrongSolutionCount { get; init; }
    public IReadOnlyDictionary<string, int> KilledCaseCountByWrongSolution { get; init; } =
        new Dictionary<string, int>();
    public IReadOnlyList<string> SurvivingWrongSolutions { get; init; } = [];
    public string Toolchain { get; init; } = string.Empty;
}
