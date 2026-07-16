using AlgoJudge.API.ErrorHandling;
using AlgoJudge.API.Security;
using Microsoft.AspNetCore.Antiforgery;

namespace AlgoJudge.API.Middleware;

public sealed class AntiforgeryValidationMiddleware
{
    private readonly RequestDelegate _next;

    public AntiforgeryValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery)
    {
        if (!RequiresValidation(context.Request))
        {
            await _next(context);
            return;
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context);
            await _next(context);
        }
        catch (AntiforgeryValidationException)
        {
            await ProblemDetailsResponse.WriteAsync(
                context,
                StatusCodes.Status403Forbidden,
                "Request verification failed.",
                ApiErrorContract.CsrfType,
                "The antiforgery token is missing or invalid.");
        }
    }

    private static bool RequiresValidation(HttpRequest request)
    {
        if (!request.Path.StartsWithSegments("/api") ||
            HttpMethods.IsGet(request.Method) ||
            HttpMethods.IsHead(request.Method) ||
            HttpMethods.IsOptions(request.Method) ||
            HttpMethods.IsTrace(request.Method))
        {
            return false;
        }

        var authorization = request.Headers.Authorization.ToString();
        return !authorization.StartsWith(
            "Bearer ",
            StringComparison.OrdinalIgnoreCase);
    }
}
