namespace AlgoJudge.ContentTool.Workspace;

public sealed class ContentCatalog
{
    public int SchemaVersion { get; init; }
    public IReadOnlyList<ContentCatalogProblem> Problems { get; init; } = [];
}
