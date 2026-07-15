namespace AlgoJudge.Application.Contracts.Problems
{
    public sealed class ProblemSampleResponse
    {
        public string Input { get; init; } = string.Empty;
        public string ExpectedOutput { get; init; } = string.Empty;
        public string? Explanation { get; init; }
        public int Ordinal { get; init; }
    }
}
