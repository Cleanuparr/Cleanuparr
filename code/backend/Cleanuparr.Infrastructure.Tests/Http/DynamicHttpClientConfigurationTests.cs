using System.Net;
using Cleanuparr.Infrastructure.Http.DynamicHttpClientSystem;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Http;

public sealed class DynamicHttpClientConfigurationTests
{
    [Fact]
    public void IsRetryable_ShouldReturnFalse_WhenResponseIsConflict()
    {
        using HttpResponseMessage response = new(HttpStatusCode.Conflict);

        DynamicHttpClientConfiguration.IsRetryable(response, excludeUnauthorized: true).ShouldBeFalse();
        DynamicHttpClientConfiguration.IsRetryable(response, excludeUnauthorized: false).ShouldBeFalse();
    }

    [Fact]
    public void IsRetryable_ShouldReturnFalse_WhenResponseIsSuccessful()
    {
        using HttpResponseMessage response = new(HttpStatusCode.OK);

        DynamicHttpClientConfiguration.IsRetryable(response, excludeUnauthorized: true).ShouldBeFalse();
    }

    [Fact]
    public void IsRetryable_ShouldRespectExcludeUnauthorized()
    {
        using HttpResponseMessage response = new(HttpStatusCode.Unauthorized);

        DynamicHttpClientConfiguration.IsRetryable(response, excludeUnauthorized: true).ShouldBeFalse();
        DynamicHttpClientConfiguration.IsRetryable(response, excludeUnauthorized: false).ShouldBeTrue();
    }

    [Fact]
    public void IsRetryable_ShouldReturnTrue_WhenResponseIsServerError()
    {
        using HttpResponseMessage response = new(HttpStatusCode.InternalServerError);

        DynamicHttpClientConfiguration.IsRetryable(response, excludeUnauthorized: true).ShouldBeTrue();
    }
}
