namespace AlgoJudge.Application.Models.ContentGeneration;

public sealed class ContentGenerationException : Exception
{
    public ContentGenerationException(string errorCode, string safeMessage, Exception? innerException = null)
        : base(safeMessage, innerException)
    {
        ErrorCode = errorCode;
        SafeMessage = safeMessage;
    }

    public string ErrorCode { get; }
    public string SafeMessage { get; }
}
