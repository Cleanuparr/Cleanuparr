using Microsoft.AspNetCore.Mvc;

namespace Cleanuparr.Api.Extensions;

public static class ControllerBaseExtensions
{
    /// <summary>
    /// Builds an RFC 9457 problem-details error response for a direct (non-throwing) controller return.
    /// Mirrors the shape produced by <see cref="Middleware.GlobalExceptionHandler"/> so every error
    /// response carries the same <c>application/problem+json</c> body and <c>traceId</c>. The
    /// <c>traceId</c> extension is added by the shared <c>CustomizeProblemDetails</c> hook inside
    /// <see cref="ProblemDetailsFactory.CreateProblemDetails"/>.
    /// </summary>
    public static ObjectResult ProblemResult(
        this ControllerBase controller,
        int statusCode,
        string detail,
        string? title = null,
        IReadOnlyDictionary<string, object?>? extensions = null)
    {
        ProblemDetails problemDetails = controller.ProblemDetailsFactory
            .CreateProblemDetails(controller.HttpContext, statusCode: statusCode, title: title, detail: detail);

        if (extensions is not null)
        {
            foreach (KeyValuePair<string, object?> extension in extensions)
            {
                problemDetails.Extensions[extension.Key] = extension.Value;
            }
        }

        return new ObjectResult(problemDetails)
        {
            StatusCode = statusCode,
            ContentTypes = { "application/problem+json" },
        };
    }
}
