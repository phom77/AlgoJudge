namespace AlgoJudge.ContentTool.Generation;

public sealed class GeneratorManifest
{
    public int SchemaVersion { get; init; }
    public DotNetComponentManifest Generator { get; init; } = new();
    public DotNetComponentManifest InputValidator { get; init; } = new();
    public IReadOnlyList<GeneratorGroupManifest> Groups { get; init; } = [];
    public ReferenceSolutionManifest ReferenceSolution { get; init; } = new();
}
