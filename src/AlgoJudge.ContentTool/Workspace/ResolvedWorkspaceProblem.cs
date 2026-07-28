using System.Text.Json;
using AlgoJudge.Application.ContentGeneration;

namespace AlgoJudge.ContentTool.Workspace;

public sealed class ResolvedWorkspaceProblem
{
    public int SchemaVersion { get; init; } = 1;
    public string CatalogPath { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string Template { get; init; } = string.Empty;
    public ResolvedProblemMetadata Metadata { get; init; } = new();
    public JsonElement GeneratorParameters { get; init; }
    public ProblemAuthoringDefinition Definition { get; init; } = new();
    public ResolvedSourceOrigins SourceOrigins { get; init; } = new();
    public string ContentHash { get; init; } = string.Empty;
}
