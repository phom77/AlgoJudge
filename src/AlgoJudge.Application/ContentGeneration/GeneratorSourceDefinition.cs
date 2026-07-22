namespace AlgoJudge.Application.ContentGeneration;

public sealed class GeneratorSourceDefinition
{
    public string Language { get; init; } = string.Empty;
    public int SdkVersion { get; init; }
    public string Source { get; init; } = string.Empty;
}
