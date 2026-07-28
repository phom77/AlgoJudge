using System.Text.Json;
using AlgoJudge.Application.ContentGeneration;
using AlgoJudge.Domain.Enums;

namespace AlgoJudge.ContentTool.Workspace;

public sealed class ContentTemplateManifest
{
    public int SchemaVersion { get; init; }
    public ProblemExecutionMode? ExecutionMode { get; init; }
    public string? Language { get; init; }
    public int? GeneratorSdkVersion { get; init; }
    public int? TimeLimitMs { get; init; }
    public int? MemoryLimitKb { get; init; }
    public SuiteQualityPolicy? QualityPolicy { get; init; }
    public JsonElement GeneratorParametersSchema { get; init; }
}
