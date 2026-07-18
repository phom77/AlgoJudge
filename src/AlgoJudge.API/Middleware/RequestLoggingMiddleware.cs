using System.Diagnostics;

namespace AlgoJudge.API.Middleware;

public sealed class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var startedAt = Stopwatch.GetTimestamp();
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["TraceId"] = context.TraceIdentifier
        });

        try
        {
            await next(context);
        }
        finally
        {
            var logLevel = context.Response.StatusCode switch
            {
                >= StatusCodes.Status500InternalServerError => LogLevel.Error,
                >= StatusCodes.Status400BadRequest => LogLevel.Warning,
                _ => LogLevel.Information
            };
            logger.Log(
                logLevel,
                "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {ElapsedMilliseconds:F1} ms.",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}
