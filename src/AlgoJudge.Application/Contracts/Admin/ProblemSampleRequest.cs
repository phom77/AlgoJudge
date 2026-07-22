namespace AlgoJudge.Application.Contracts.Admin;

public sealed class ProblemSampleRequest
{
    public string Input { get; init; } = string.Empty;
    public string ExpectedOutput { get; init; } = string.Empty;
    public string? Explanation { get; init; }
}
