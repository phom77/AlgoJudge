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
        var problemDetails = ApiErrorContract.Create(
            context,
            status,
            title,
            type,
            detail);

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
