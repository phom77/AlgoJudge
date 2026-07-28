using System.Text.Json;

namespace AlgoJudge.ContentTool.Workspace;

public sealed class ContentProblemSample
{
    public JsonElement Arguments { get; init; }
    public JsonElement Expected { get; init; }
    public string? Explanation { get; init; }
}
