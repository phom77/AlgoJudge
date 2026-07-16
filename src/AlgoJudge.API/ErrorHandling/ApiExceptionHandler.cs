using AlgoJudge.Application.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AlgoJudge.API.ErrorHandling;

public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, type, detail) = exception switch
        {
            RequestValidationException => (
                StatusCodes.Status400BadRequest,
                "The request is invalid.",
                "urn:algojudge:error:validation",
                exception.Message),
            AuthenticationException => (
                StatusCodes.Status401Unauthorized,
                "Authentication failed.",
                "urn:algojudge:error:authentication",
                exception.Message),
            ForbiddenException => (
                StatusCodes.Status403Forbidden,
                "Access is forbidden.",
                "urn:algojudge:error:forbidden",
                exception.Message),
            ResourceNotFoundException => (
                StatusCodes.Status404NotFound,
                "The requested resource was not found.",
                "urn:algojudge:error:not-found",
                exception.Message),
            ConflictException or DbUpdateException
                {
                    InnerException: PostgresException
                    {
                        SqlState: PostgresErrorCodes.UniqueViolation
                    }
                } => (
                StatusCodes.Status409Conflict,
                "The request conflicts with the current state.",
                "urn:algojudge:error:conflict",
                exception is ConflictException ? exception.Message : "A conflicting resource already exists."),
            _ => (
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.",
                "urn:algojudge:error:internal",
                "The server could not complete the request.")
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                exception,
                "Unhandled exception while processing {RequestMethod} {RequestPath}.",
                httpContext.Request.Method,
                httpContext.Request.Path);
        }
        else
        {
            logger.LogWarning(
                "Request failed with status {StatusCode}: {ExceptionMessage}",
                status,
                exception.Message);
        }

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Type = type,
                Detail = detail,
                Instance = httpContext.Request.Path
            },
            Exception = exception
        });
    }
}
