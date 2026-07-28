using System.Text.Json;
using AlgoJudge.Application.ContentGeneration;
using AlgoJudge.Application.FunctionExecution;
using AlgoJudge.Domain.Enums;

namespace AlgoJudge.ContentTool.Workspace;

public sealed class ContentProblemManifest
{
    public int SchemaVersion { get; init; }
    public string Template { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public DifficultyLevel Difficulty { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public string Statement { get; init; } = string.Empty;
    public IReadOnlyList<string> Constraints { get; init; } = [];
    public int? TimeLimitMs { get; init; }
    public int? MemoryLimitKb { get; init; }
    public FunctionSignature Signature { get; init; } = new();
    public IReadOnlyList<ContentProblemSample> Samples { get; init; } = [];
    public JsonElement GeneratorParameters { get; init; }
    public SuiteQualityPolicy? QualityPolicy { get; init; }
}
