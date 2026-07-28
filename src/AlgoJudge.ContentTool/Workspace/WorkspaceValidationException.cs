namespace AlgoJudge.ContentTool.Workspace;

public sealed class WorkspaceValidationException : Exception
{
    public WorkspaceValidationException(IEnumerable<string> errors)
        : base("The content workspace is invalid.")
    {
        Errors = errors
            .Where(error => !string.IsNullOrWhiteSpace(error))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<string> Errors { get; }
}
