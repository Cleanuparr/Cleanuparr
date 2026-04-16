using System.Net;
using Cleanuparr.Infrastructure.Features.Notifications.Pushover;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Shared.Helpers;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications.Pushover;

public class PushoverProxyTests
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly FakeHttpMessageHandler _httpMessageHandler;

    public PushoverProxyTests()
    {
        _httpMessageHandler = new FakeHttpMessageHandler();
        _httpClientFactory = Substitute.For<IHttpClientFactory>();

        var httpClient = new HttpClient(_httpMessageHandler);
        _httpClientFactory
            .CreateClient(Constants.HttpClientWithRetryName)
            .Returns(httpClient);
    }

    private PushoverProxy CreateProxy()
    {
        return new PushoverProxy(_httpClientFactory);
    }

    private static PushoverPayload CreatePayload(int priority = 0)
    {
        return new PushoverPayload
        {
            Token = "test-token",
            User = "test-user",
            Message = "Test message",
            Title = "Test Title",
            Priority = priority,
            Retry = priority == 2 ? 60 : null,
            Expire = priority == 2 ? 3600 : null
        };
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidFactory_CreatesInstance()
    {
        // Act
        var proxy = CreateProxy();

        // Assert
        proxy.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_CreatesHttpClientWithCorrectName()
    {
        // Act
        _ = CreateProxy();

        // Assert
        _httpClientFactory.Received(1).CreateClient(Constants.HttpClientWithRetryName);
    }

    #endregion

    #region SendNotification Success Tests

    [Fact]
    public async Task SendNotification_WhenSuccessful_CompletesWithoutException()
    {
        // Arrange
        var proxy = CreateProxy();
        SetupSuccessResponse();

        // Act & Assert - Should not throw
        await proxy.SendNotification(CreatePayload());
    }

    [Fact]
    public async Task SendNotification_SendsPostRequest()
    {
        // Arrange
        var proxy = CreateProxy();
        SetupSuccessResponse();

        // Act
        await proxy.SendNotification(CreatePayload());

        // Assert
        _httpMessageHandler.CapturedRequests[0].Method.ShouldBe(HttpMethod.Post);
    }

    [Fact]
    public async Task SendNotification_SendsToCorrectUrl()
    {
        // Arrange
        var proxy = CreateProxy();
        SetupSuccessResponse();

        // Act
        await proxy.SendNotification(CreatePayload());

        // Assert
        _httpMessageHandler.CapturedRequests[0].RequestUri?.ToString().ShouldBe("https://api.pushover.net/1/messages.json");
    }

    [Fact]
    public async Task SendNotification_UsesFormUrlEncodedContent()
    {
        // Arrange
        var proxy = CreateProxy();
        SetupSuccessResponse();

        // Act
        await proxy.SendNotification(CreatePayload());

        // Assert
        _httpMessageHandler.CapturedRequests[0].Content?.Headers.ContentType?.MediaType.ShouldBe("application/x-www-form-urlencoded");
    }

    [Fact]
    public async Task SendNotification_IncludesRequiredFieldsInPayload()
    {
        // Arrange
        var proxy = CreateProxy();
        SetupSuccessResponse();

        var payload = CreatePayload();

        // Act
        await proxy.SendNotification(payload);

        // Assert
        var capturedContent = _httpMessageHandler.CapturedRequestBodies[0]!;
        capturedContent.ShouldContain("token=test-token");
        capturedContent.ShouldContain("user=test-user");
        capturedContent.ShouldContain("message=Test+message");
        capturedContent.ShouldContain("priority=0");
    }

    [Fact]
    public async Task SendNotification_WithEmergencyPriority_IncludesRetryAndExpire()
    {
        // Arrange
        var proxy = CreateProxy();
        SetupSuccessResponse();

        var payload = CreatePayload(priority: 2); // Emergency

        // Act
        await proxy.SendNotification(payload);

        // Assert
        var capturedContent = _httpMessageHandler.CapturedRequestBodies[0]!;
        capturedContent.ShouldContain("retry=60");
        capturedContent.ShouldContain("expire=3600");
    }

    [Fact]
    public async Task SendNotification_WithNonEmergencyPriority_DoesNotIncludeRetryAndExpire()
    {
        // Arrange
        var proxy = CreateProxy();
        SetupSuccessResponse();

        var payload = CreatePayload(priority: 1); // High, not Emergency

        // Act
        await proxy.SendNotification(payload);

        // Assert
        var capturedContent = _httpMessageHandler.CapturedRequestBodies[0]!;
        capturedContent.ShouldNotContain("retry=");
        capturedContent.ShouldNotContain("expire=");
    }

    [Fact]
    public async Task SendNotification_WithSound_IncludesSound()
    {
        // Arrange
        var proxy = CreateProxy();
        SetupSuccessResponse();

        var payload = new PushoverPayload
        {
            Token = "test-token",
            User = "test-user",
            Message = "Test message",
            Title = "Test Title",
            Priority = 0,
            Sound = "cosmic"
        };

        // Act
        await proxy.SendNotification(payload);

        // Assert
        var capturedContent = _httpMessageHandler.CapturedRequestBodies[0]!;
        capturedContent.ShouldContain("sound=cosmic");
    }

    [Fact]
    public async Task SendNotification_WithDevice_IncludesDevice()
    {
        // Arrange
        var proxy = CreateProxy();
        SetupSuccessResponse();

        var payload = new PushoverPayload
        {
            Token = "test-token",
            User = "test-user",
            Message = "Test message",
            Title = "Test Title",
            Priority = 0,
            Device = "my-phone"
        };

        // Act
        await proxy.SendNotification(payload);

        // Assert
        var capturedContent = _httpMessageHandler.CapturedRequestBodies[0]!;
        capturedContent.ShouldContain("device=my-phone");
    }

    [Fact]
    public async Task SendNotification_WithTags_IncludesTags()
    {
        // Arrange
        var proxy = CreateProxy();
        SetupSuccessResponse();

        var payload = new PushoverPayload
        {
            Token = "test-token",
            User = "test-user",
            Message = "Test message",
            Title = "Test Title",
            Priority = 0,
            Tags = "tag1,tag2"
        };

        // Act
        await proxy.SendNotification(payload);

        // Assert
        var capturedContent = _httpMessageHandler.CapturedRequestBodies[0]!;
        capturedContent.ShouldContain("tags=tag1%2Ctag2"); // URL-encoded comma
    }

    #endregion

    #region SendNotification Error Tests

    [Fact]
    public async Task SendNotification_When400_ThrowsPushoverExceptionWithBadRequest()
    {
        // Arrange
        var proxy = CreateProxy();
        SetupErrorResponse(HttpStatusCode.BadRequest, "{\"status\":0,\"errors\":[\"invalid token\"]}");

        // Act & Assert
        var ex = await Should.ThrowAsync<PushoverException>(() =>
            proxy.SendNotification(CreatePayload()));
        ex.Message.ShouldContain("Bad request");
    }

    [Fact]
    public async Task SendNotification_When401_ThrowsPushoverExceptionWithUnauthorized()
    {
        // Arrange
        var proxy = CreateProxy();
        SetupErrorResponse(HttpStatusCode.Unauthorized, "{\"status\":0,\"errors\":[\"invalid api key\"]}");

        // Act & Assert
        var ex = await Should.ThrowAsync<PushoverException>(() =>
            proxy.SendNotification(CreatePayload()));
        ex.Message.ShouldContain("Invalid API token or user key");
    }

    [Fact]
    public async Task SendNotification_When429_ThrowsPushoverExceptionWithRateLimited()
    {
        // Arrange
        var proxy = CreateProxy();
        SetupErrorResponse((HttpStatusCode)429, "{\"status\":0,\"errors\":[\"rate limit exceeded\"]}");

        // Act & Assert
        var ex = await Should.ThrowAsync<PushoverException>(() =>
            proxy.SendNotification(CreatePayload()));
        ex.Message.ShouldContain("Rate limit exceeded");
    }

    [Fact]
    public async Task SendNotification_WhenApiReturnsStatus0_ThrowsPushoverException()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"status\":0,\"errors\":[\"user key is invalid\"]}")
        }));

        // Act & Assert
        var ex = await Should.ThrowAsync<PushoverException>(() =>
            proxy.SendNotification(CreatePayload()));
        ex.Message.ShouldContain("user key is invalid");
    }

    [Fact]
    public async Task SendNotification_WhenNetworkError_ThrowsPushoverException()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupThrow(new HttpRequestException("Network error"));

        // Act & Assert
        var ex = await Should.ThrowAsync<PushoverException>(() =>
            proxy.SendNotification(CreatePayload()));
        ex.Message.ShouldContain("Unable to connect to Pushover API");
    }

    #endregion

    #region Helper Methods

    private void SetupSuccessResponse()
    {
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"status\":1,\"request\":\"abc123\"}")
        }));
    }

    private void SetupErrorResponse(HttpStatusCode statusCode, string responseBody)
    {
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(responseBody)
        }));
    }

    #endregion
}
