using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nagomi.Api.Domain;

namespace Nagomi.Api.Infrastructure.Errors;

public sealed class NagomiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<NagomiExceptionHandler> logger,
    IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            DomainValidationException => (StatusCodes.Status400BadRequest, "Validation failed", exception.Message),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "Invalid request", exception.Message),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Resource not found", exception.Message),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Concurrency conflict", "The resource changed before the operation completed."),
            DbUpdateException => (StatusCodes.Status409Conflict, "Persistence conflict", "The operation conflicts with persisted data."),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error", "An unexpected error occurred.")
        };

        if (status >= 500)
        {
            if (environment.IsDevelopment())
                logger.LogError(exception, "Unhandled development request failure. TraceId: {TraceId}", httpContext.TraceIdentifier);
            else
                logger.LogError("Unhandled request failure of type {ExceptionType}. TraceId: {TraceId}",
                    exception.GetType().Name, httpContext.TraceIdentifier);
        }
        else
        {
            logger.LogWarning("Request failed with status {StatusCode}. TraceId: {TraceId}", status, httpContext.TraceIdentifier);
        }

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Instance = httpContext.Request.Path
            }
        });
    }
}

public static class ErrorHandlingServiceExtensions
{
    public static IServiceCollection AddNagomiProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        });
        services.AddExceptionHandler<NagomiExceptionHandler>();
        return services;
    }
}
