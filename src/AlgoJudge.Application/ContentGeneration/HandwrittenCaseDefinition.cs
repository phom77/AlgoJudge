using System.Text.Json;

namespace AlgoJudge.Application.ContentGeneration;

public sealed class HandwrittenCaseDefinition
{
    public string Name { get; init; } = string.Empty;
    public string Group { get; init; } = "handwritten";
    public JsonElement Arguments { get; init; }
}
