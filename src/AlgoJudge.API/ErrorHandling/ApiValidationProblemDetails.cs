using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AlgoJudge.API.ErrorHandling;

public sealed class ApiValidationProblemDetails : ValidationProblemDetails
{
    public ApiValidationProblemDetails()
    {
    }

    public ApiValidationProblemDetails(ModelStateDictionary modelState)
        : base(modelState)
    {
    }

    public string Code { get; init; } = string.Empty;
    public string TraceId { get; init; } = string.Empty;
}
