using Cleanuparr.Api.Models;
using Cleanuparr.Domain.Exceptions;
using System.Text.Json;

namespace Cleanuparr.Api.Middleware;

public class QueueRuleValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<QueueRuleValidationMiddleware> _logger;

    public QueueRuleValidationMiddleware(RequestDelegate next, ILogger<QueueRuleValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only apply validation to queue rule endpoints
        if (!context.Request.Path.StartsWithSegments("/api/queue-rules"))
        {
            await _next(context);
            return;
        }

        // Only validate POST and PUT requests
        if (context.Request.Method != HttpMethods.Post && context.Request.Method != HttpMethods.Put)
        {
            await _next(context);
            return;
        }

        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning("Queue rule validation failed: {Message}", ex.Message);
            
            context.Response.StatusCode = 400;
            context.Response.ContentType = "application/json";
            
            var errorResponse = new
            {
                error = "Validation Error",
                message = ex.Message,
                timestamp = DateTime.UtcNow
            };
            
            var json = JsonSerializer.Serialize(errorResponse);
            await context.Response.WriteAsync(json);
        }
        catch (ArgumentException ex) when (ex.Message.Contains("ByteSize"))
        {
            _logger.LogWarning("Invalid ByteSize format in queue rule: {Message}", ex.Message);
            
            context.Response.StatusCode = 400;
            context.Response.ContentType = "application/json";
            
            var errorResponse = new
            {
                error = "Validation Error",
                message = "Invalid speed format. Please use format like '1MB/s', '500KB/s', etc.",
                timestamp = DateTime.UtcNow
            };
            
            var json = JsonSerializer.Serialize(errorResponse);
            await context.Response.WriteAsync(json);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("Invalid JSON in queue rule request: {Message}", ex.Message);
            
            context.Response.StatusCode = 400;
            context.Response.ContentType = "application/json";
            
            var errorResponse = new
            {
                error = "Invalid JSON",
                message = "The request body contains invalid JSON",
                timestamp = DateTime.UtcNow
            };
            
            var json = JsonSerializer.Serialize(errorResponse);
            await context.Response.WriteAsync(json);
        }
    }
}