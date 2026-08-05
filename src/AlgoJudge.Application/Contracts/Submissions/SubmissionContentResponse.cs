namespace AlgoJudge.Application.Contracts.Submissions;

public sealed class SubmissionContentResponse
{
    public string SourceCode { get; init; } = string.Empty;
    public string? CompileMessage { get; init; }
}
