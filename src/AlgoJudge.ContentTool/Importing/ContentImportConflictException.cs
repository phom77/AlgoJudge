namespace AlgoJudge.ContentTool.Importing;

public sealed class ContentImportConflictException : Exception
{
    public ContentImportConflictException(string message) : base(message)
    {
    }
}
