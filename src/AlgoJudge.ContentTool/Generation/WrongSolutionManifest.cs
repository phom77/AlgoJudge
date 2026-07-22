namespace AlgoJudge.ContentTool.Generation;

public sealed class WrongSolutionManifest
{
    public string Name { get; init; } = string.Empty;
    public string SourceSha256 { get; init; } = string.Empty;
    public int KilledCaseCount { get; init; }
}
