using Microsoft.AspNetCore.Mvc;

namespace AlgoJudge.API.ErrorHandling;

public sealed class ApiProblemDetails : ProblemDetails
{
    public string Code { get; init; } = string.Empty;
    public string TraceId { get; init; } = string.Empty;
}
