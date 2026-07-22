namespace AlgoJudge.ContentTool.Generation;

public sealed class GeneratedSuiteManifest
{
    public int SchemaVersion { get; init; }
    public string GeneratorManifestSha256 { get; init; } = string.Empty;
    public string GeneratorAssemblySha256 { get; init; } = string.Empty;
    public string ValidatorAssemblySha256 { get; init; } = string.Empty;
    public string ReferenceSolutionSha256 { get; init; } = string.Empty;
    public string AuthoringDefinitionSha256 { get; init; } = string.Empty;
    public string GeneratorSourceSha256 { get; init; } = string.Empty;
    public string ValidatorSourceSha256 { get; init; } = string.Empty;
    public string GenerationToolchain { get; init; } = string.Empty;
    public int GeneratorSdkVersion { get; init; }
    public string Comparator { get; init; } = string.Empty;
    public IReadOnlyList<WrongSolutionManifest> WrongSolutions { get; init; } = [];
    public IReadOnlyList<string> SurvivingWrongSolutions { get; init; } = [];
    public string SuiteSha256 { get; init; } = string.Empty;
    public IReadOnlyList<GeneratedCaseManifest> Cases { get; init; } = [];
}
