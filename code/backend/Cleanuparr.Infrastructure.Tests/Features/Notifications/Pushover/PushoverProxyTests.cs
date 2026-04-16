using System.Net;
using Cleanuparr.Infrastructure.Features.Notifications.Pushover;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Shared.Helpers;
using NSubstitute;
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
        Assert.NotNull(proxy);
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
        Assert.Equal(HttpMethod.Post, _httpMessageHandler.CapturedRequests[0].Method);
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
        Assert.Equal("https://api.pushover.net/1/messages.json", _httpMessageHandler.CapturedRequests[0].RequestUri?.ToString());
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
        Assert.Equal("application/x-www-form-urlencoded", _httpMessageHandler.CapturedRequests[0].Content?.Headers.ContentType?.MediaType);
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
        Assert.Contains("token=test-token", capturedContent);
        Assert.Contains("user=test-user", capturedContent);
        Assert.Contains("message=Test+message", capturedContent);
        Assert.Contains("priority=0", capturedContent);
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
        Assert.Contains("retry=60", capturedContent);
        Assert.Contains("expire=3600", capturedContent);
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
        Assert.DoesNotContain("retry=", capturedContent);
        Assert.DoesNotContain("expire=", capturedContent);
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
        Assert.Contains("sound=cosmic", capturedContent);
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
        Assert.Contains("device=my-phone", capturedContent);
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
        Assert.Contains("tags=tag1%2Ctag2", capturedContent); // URL-encoded comma
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
        var ex = await Assert.ThrowsAsync<PushoverException>(() =>
            proxy.SendNotification(CreatePayload()));
        Assert.Contains("Bad request", ex.Message);
    }

    [Fact]
    public async Task SendNotification_When401_ThrowsPushoverExceptionWithUnauthorized()
    {
        // Arrange
        var proxy = CreateProxy();
        SetupErrorResponse(HttpStatusCode.Unauthorized, "{\"status\":0,\"errors\":[\"invalid api key\"]}");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<PushoverException>(() =>
            proxy.SendNotification(CreatePayload()));
        Assert.Contains("Invalid API token or user key", ex.Message);
    }

    [Fact]
    public async Task SendNotification_When429_ThrowsPushoverExceptionWithRateLimited()
    {
        // Arrange
        var proxy = CreateProxy();
        SetupErrorResponse((HttpStatusCode)429, "{\"status\":0,\"errors\":[\"rate limit exceeded\"]}");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<PushoverException>(() =>
            proxy.SendNotification(CreatePayload()));
        Assert.Contains("Rate limit exceeded", ex.Message);
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
        var ex = await Assert.ThrowsAsync<PushoverException>(() =>
            proxy.SendNotification(CreatePayload()));
        Assert.Contains("user key is invalid", ex.Message);
    }

    [Fact]
    public async Task SendNotification_WhenNetworkError_ThrowsPushoverException()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupThrow(new HttpRequestException("Network error"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<PushoverException>(() =>
            proxy.SendNotification(CreatePayload()));
        Assert.Contains("Unable to connect to Pushover API", ex.Message);
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
