using Cleanuparr.Api.DependencyInjection;
using Cleanuparr.Api.Middleware;
using Cleanuparr.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace Cleanuparr.Api.Tests.Middleware;

public class GlobalExceptionHandlerTests
{
    private static readonly ProblemDetailsFactory ProblemDetailsFactory = BuildProblemDetailsFactory();

    private static ProblemDetailsFactory BuildProblemDetailsFactory()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddControllers();
        services.AddCleanuparrProblemDetails();
        return services.BuildServiceProvider().GetRequiredService<ProblemDetailsFactory>();
    }

    private static async Task<(bool handled, HttpContext context, ProblemDetails problemDetails)> Handle(Exception exception)
    {
        IProblemDetailsService problemDetailsService = Substitute.For<IProblemDetailsService>();
        problemDetailsService
            .TryWriteAsync(Arg.Any<ProblemDetailsContext>())
            .Returns(callInfo => ValueTask.FromResult(true));

        DefaultHttpContext context = new();
        GlobalExceptionHandler handler = new(problemDetailsService, ProblemDetailsFactory, NullLogger<GlobalExceptionHandler>.Instance);

        bool handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        ProblemDetailsContext captured = (ProblemDetailsContext)problemDetailsService
            .ReceivedCalls()
            .Single()
            .GetArguments()[0]!;

        return (handled, context, captured.ProblemDetails);
    }

    [Fact]
    public async Task ValidationException_MapsTo400_WithMessageAsDetail()
    {
        (bool handled, HttpContext context, ProblemDetails problemDetails) = await Handle(new ValidationException("Name is required"));

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        problemDetails.Status.ShouldBe(StatusCodes.Status400BadRequest);
        problemDetails.Title.ShouldBe("Validation failed");
        problemDetails.Detail.ShouldBe("Name is required");
        problemDetails.Type.ShouldNotBeNullOrEmpty();
        problemDetails.Extensions.ShouldContainKey("traceId");
    }

    [Fact]
    public async Task NotificationTestException_MapsTo400()
    {
        (bool handled, HttpContext context, ProblemDetails problemDetails) = await Handle(new NotificationTestException("Test failed: connection refused"));

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        problemDetails.Status.ShouldBe(StatusCodes.Status400BadRequest);
        problemDetails.Title.ShouldBe("Notification test failed");
        problemDetails.Detail.ShouldBe("Test failed: connection refused");
    }

    [Fact]
    public async Task RateLimitException_MapsTo429_WithRetryAfterExtensionAndHeader()
    {
        (bool handled, HttpContext context, ProblemDetails problemDetails) = await Handle(new RateLimitException("Account is locked", 30));

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status429TooManyRequests);
        problemDetails.Status.ShouldBe(StatusCodes.Status429TooManyRequests);
        problemDetails.Title.ShouldBe("Too many requests");
        problemDetails.Extensions["retryAfterSeconds"].ShouldBe(30);
        context.Response.Headers.RetryAfter.ToString().ShouldBe("30");
    }

    [Fact]
    public async Task RateLimitException_WithZeroRetry_MapsTo429_WithoutRetryAfter()
    {
        (bool handled, HttpContext context, ProblemDetails problemDetails) = await Handle(new RateLimitException("Too many pending OIDC flows", 0));

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status429TooManyRequests);
        problemDetails.Extensions.ShouldNotContainKey("retryAfterSeconds");
        context.Response.Headers.RetryAfter.ToString().ShouldBeEmpty();
    }

    [Fact]
    public async Task UnknownException_MapsTo500_WithGenericDetail_AndDoesNotLeakMessage()
    {
        (bool handled, HttpContext context, ProblemDetails problemDetails) = await Handle(new InvalidOperationException("internal connection string leaked"));

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
        problemDetails.Status.ShouldBe(StatusCodes.Status500InternalServerError);
        problemDetails.Detail.ShouldBe("An unexpected error occurred");
        problemDetails.Detail.ShouldNotContain("connection string");
    }
}
