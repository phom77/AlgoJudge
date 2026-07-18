namespace AlgoJudge.ContentTool.Publishing;

public sealed class ContentPublicationConflictException : Exception
{
    public ContentPublicationConflictException(string message) : base(message)
    {
    }
}
