namespace AlgoJudge.ContentTool.Generation;

public sealed class GeneratedSuiteManifest
{
    public int SchemaVersion { get; init; }
    public string GeneratorManifestSha256 { get; init; } = string.Empty;
    public string GeneratorAssemblySha256 { get; init; } = string.Empty;
    public string ValidatorAssemblySha256 { get; init; } = string.Empty;
    public string ReferenceSolutionSha256 { get; init; } = string.Empty;
    public string SuiteSha256 { get; init; } = string.Empty;
    public IReadOnlyList<GeneratedCaseManifest> Cases { get; init; } = [];
}
