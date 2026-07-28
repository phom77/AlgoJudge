namespace AlgoJudge.ContentTool.Workspace;

public sealed class ContentWorkspaceResolution
{
    public int SchemaVersion { get; init; } = 1;
    public string CatalogPath { get; init; } = string.Empty;
    public IReadOnlyList<ResolvedWorkspaceProblem> Problems { get; init; } = [];
}
