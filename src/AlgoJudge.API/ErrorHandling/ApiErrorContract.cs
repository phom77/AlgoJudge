using Microsoft.AspNetCore.Mvc;

namespace AlgoJudge.API.ErrorHandling;

public static class ApiErrorContract
{
    public const string ValidationType = "urn:algojudge:error:validation";
    public const string AuthenticationType = "urn:algojudge:error:authentication";
    public const string ForbiddenType = "urn:algojudge:error:forbidden";
    public const string CsrfType = "urn:algojudge:error:csrf";
    public const string NotFoundType = "urn:algojudge:error:not-found";
    public const string ConflictType = "urn:algojudge:error:conflict";
    public const string RateLimitType = "urn:algojudge:error:rate-limit";
    public const string InternalType = "urn:algojudge:error:internal";

    public static ApiProblemDetails Create(
        HttpContext context,
        int status,
        string title,
        string type,
        string detail)
    {
        return new ApiProblemDetails
        {
            Status = status,
            Title = title,
            Type = type,
            Detail = detail,
            Instance = context.Request.Path,
            Code = GetCode(type),
            TraceId = context.TraceIdentifier
        };
    }

    public static ApiValidationProblemDetails CreateValidation(
        ActionContext context)
    {
        return new ApiValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "The request is invalid.",
            Type = ValidationType,
            Detail = "One or more validation errors occurred.",
            Instance = context.HttpContext.Request.Path,
            Code = GetCode(ValidationType),
            TraceId = context.HttpContext.TraceIdentifier
        };
    }

    public static string GetCode(string? type)
    {
        return type switch
        {
            ValidationType => "validation",
            AuthenticationType => "authentication",
            ForbiddenType => "forbidden",
            CsrfType => "csrf",
            NotFoundType => "not-found",
            ConflictType => "conflict",
            RateLimitType => "rate-limit",
            InternalType => "internal",
            _ => "unknown"
        };
    }
}
