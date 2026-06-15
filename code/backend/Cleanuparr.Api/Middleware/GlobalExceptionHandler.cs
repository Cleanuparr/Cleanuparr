using Cleanuparr.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Cleanuparr.Api.Middleware;

/// <summary>
/// Single source of truth for mapping unhandled exceptions to RFC 9457 problem-details responses.
/// Registered via <c>AddExceptionHandler</c> + <c>UseExceptionHandler</c>.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
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

        if (status >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled error during request to {Path}", context.Request.Path);
        }
        else
        {
            _logger.LogWarning(exception, "Handled {Status} during request to {Path}: {Message}",
                status, context.Request.Path, exception.Message);
        }

        context.Response.StatusCode = status;

        ProblemDetails problemDetails = new()
        {
            Status = status,
            Title = title,
            Detail = detail,
        };

        if (exception is RateLimitException rateLimitException)
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
}
