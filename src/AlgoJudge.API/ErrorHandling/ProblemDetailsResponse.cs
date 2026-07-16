using Microsoft.AspNetCore.Mvc;

namespace AlgoJudge.API.ErrorHandling;

public static class ProblemDetailsResponse
{
    public static async Task WriteAsync(
        HttpContext context,
        int status,
        string title,
        string type,
        string detail)
    {
        context.Response.StatusCode = status;
        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = title,
            Type = type,
            Detail = detail,
            Instance = context.Request.Path
        };

        var service = context.RequestServices.GetRequiredService<IProblemDetailsService>();
        var written = await service.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problemDetails
        });

        if (!written)
            await context.Response.WriteAsJsonAsync(problemDetails, context.RequestAborted);
    }
}
