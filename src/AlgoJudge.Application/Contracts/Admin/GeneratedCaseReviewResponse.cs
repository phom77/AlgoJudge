namespace AlgoJudge.Application.Contracts.Admin;

public sealed class GeneratedCaseReviewResponse
{
    public int Ordinal { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Group { get; init; } = string.Empty;
    public int Seed { get; init; }
    public IReadOnlyList<string> KilledWrongSolutions { get; init; } = [];
}
