using Cleanuparr.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Cleanuparr.Api.Middleware;

/// <summary>
/// Single source of truth for mapping unhandled exceptions to RFC 9457 problem-details responses.
/// Registered via <c>AddExceptionHandler</c> + <c>UseExceptionHandler</c>.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ProblemDetailsFactory _problemDetailsFactory;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(
        IProblemDetailsService problemDetailsService,
        ProblemDetailsFactory problemDetailsFactory,
        ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _problemDetailsFactory = problemDetailsFactory;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        (int status, string title, string detail) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed", exception.Message),
            NotificationTestException => (StatusCodes.Status400BadRequest, "Notification test failed", exception.Message),
            RateLimitException => (StatusCodes.Status429TooManyRequests, "Too many requests", exception.Message),
            _ => (StatusCodes.Status500InternalServerError, "An error occurred", "An unexpected error occurred"),
        };

        string path = Sanitize(context.Request.Path);

        if (status >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled error during request to {Path}", path);
        }
        else
        {
            _logger.LogWarning(exception, "Handled {Status} during request to {Path}: {Message}",
                status, path, Sanitize(exception.Message));
        }

        context.Response.StatusCode = status;

        ProblemDetails problemDetails = _problemDetailsFactory.CreateProblemDetails(
            context, statusCode: status, title: title, detail: detail);

        if (exception is RateLimitException { RetryAfterSeconds: > 0 } rateLimitException)
        {
            problemDetails.Extensions["retryAfterSeconds"] = rateLimitException.RetryAfterSeconds;
            context.Response.Headers.RetryAfter = rateLimitException.RetryAfterSeconds.ToString();
        }

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problemDetails,
            Exception = exception,
        });
    }

    /// <summary>
    /// Strips line breaks from user-controlled values before they reach the logs to prevent log forging.
    /// </summary>
    private static string Sanitize(string? value)
    {
        return value is null ? string.Empty : value.Replace("\r", string.Empty).Replace("\n", string.Empty);
    }
}
