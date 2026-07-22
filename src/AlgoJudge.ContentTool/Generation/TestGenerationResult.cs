namespace AlgoJudge.ContentTool.Generation;

public sealed record TestGenerationResult(int TestCaseCount, string SuiteSha256)
{
    public int SurvivingWrongSolutionCount { get; init; }
}
