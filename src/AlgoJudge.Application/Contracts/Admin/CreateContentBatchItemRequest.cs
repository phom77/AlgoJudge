using System.Text.Json;
using AlgoJudge.Application.ContentGeneration;
using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Application.Contracts.Admin;

public sealed class CreateContentBatchItemRequest
{
    public string CatalogPath { get; init; } = string.Empty;
    public ContentBatchImportAction Action { get; init; }
    public string ContentHash { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string StatementMarkdown { get; init; } = string.Empty;
    public string ConstraintsMarkdown { get; init; } = string.Empty;
    public DifficultyLevel Difficulty { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public int TimeLimitMs { get; init; }
    public int MemoryLimitKb { get; init; }
    public IReadOnlyList<ProblemSampleRequest> Samples { get; init; } = [];
    public JsonElement GeneratorParameters { get; init; }
    public ProblemAuthoringDefinition? Definition { get; init; }
    public string? ValidationFailureCategory { get; init; }
    public string? ValidationFailureMessage { get; init; }
}
