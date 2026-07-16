using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

namespace AlgoJudge.Worker;

public static class WorkerHealthResponseWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var response = new
        {
            status = report.Status.ToString(),
            durationMilliseconds = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => new
                {
                    name = entry.Key,
                    status = entry.Value.Status.ToString(),
                    durationMilliseconds = entry.Value.Duration.TotalMilliseconds,
                    description = entry.Value.Description
                })
        };

        return JsonSerializer.SerializeAsync(
            context.Response.Body,
            response,
            cancellationToken: context.RequestAborted);
    }
}
