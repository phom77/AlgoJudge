namespace AlgoJudge.ContentTool.Generation;

public sealed class GeneratedCaseManifest
{
    public int Ordinal { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Group { get; init; } = string.Empty;
    public int Seed { get; init; }
    public string InputSha256 { get; init; } = string.Empty;
    public string OutputSha256 { get; init; } = string.Empty;
    public IReadOnlyList<string> KilledWrongSolutions { get; init; } = [];
}
