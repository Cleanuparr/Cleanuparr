using System.Net;
using Cleanuparr.Infrastructure.Features.Notifications.Pushover;
using Cleanuparr.Shared.Helpers;
using Moq;
using Moq.Protected;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications.Pushover;

public class PushoverProxyTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;

    public PushoverProxyTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _httpClientFactoryMock
            .Setup(f => f.CreateClient(Constants.HttpClientWithRetryName))
            .Returns(httpClient);
    }

    private PushoverProxy CreateProxy()
    {
        return new PushoverProxy(_httpClientFactoryMock.Object);
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
        _httpClientFactoryMock.Verify(f => f.CreateClient(Constants.HttpClientWithRetryName), Times.Once);
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
        HttpMethod? capturedMethod = null;

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedMethod = req.Method)
            .ReturnsAsync(CreateSuccessResponse());

        // Act
        await proxy.SendNotification(CreatePayload());

        // Assert
        Assert.Equal(HttpMethod.Post, capturedMethod);
    }

    [Fact]
    public async Task SendNotification_SendsToCorrectUrl()
    {
        // Arrange
        var proxy = CreateProxy();
        Uri? capturedUri = null;

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedUri = req.RequestUri)
            .ReturnsAsync(CreateSuccessResponse());

        // Act
        await proxy.SendNotification(CreatePayload());

        // Assert
        Assert.NotNull(capturedUri);
        Assert.Equal("https://api.pushover.net/1/messages.json", capturedUri.ToString());
    }

    [Fact]
    public async Task SendNotification_UsesFormUrlEncodedContent()
    {
        // Arrange
        var proxy = CreateProxy();
        string? capturedContentType = null;

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
                capturedContentType = req.Content?.Headers.ContentType?.MediaType)
            .ReturnsAsync(CreateSuccessResponse());

        // Act
        await proxy.SendNotification(CreatePayload());

        // Assert
        Assert.Equal("application/x-www-form-urlencoded", capturedContentType);
    }

    [Fact]
    public async Task SendNotification_IncludesRequiredFieldsInPayload()
    {
        // Arrange
        var proxy = CreateProxy();
        string? capturedContent = null;

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
                capturedContent = await req.Content!.ReadAsStringAsync())
            .ReturnsAsync(CreateSuccessResponse());

        var payload = CreatePayload();

        // Act
        await proxy.SendNotification(payload);

        // Assert
        Assert.NotNull(capturedContent);
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
        string? capturedContent = null;

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
                capturedContent = await req.Content!.ReadAsStringAsync())
            .ReturnsAsync(CreateSuccessResponse());

        var payload = CreatePayload(priority: 2); // Emergency

        // Act
        await proxy.SendNotification(payload);

        // Assert
        Assert.NotNull(capturedContent);
        Assert.Contains("retry=60", capturedContent);
        Assert.Contains("expire=3600", capturedContent);
    }

    [Fact]
    public async Task SendNotification_WithNonEmergencyPriority_DoesNotIncludeRetryAndExpire()
    {
        // Arrange
        var proxy = CreateProxy();
        string? capturedContent = null;

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
                capturedContent = await req.Content!.ReadAsStringAsync())
            .ReturnsAsync(CreateSuccessResponse());

        var payload = CreatePayload(priority: 1); // High, not Emergency

        // Act
        await proxy.SendNotification(payload);

        // Assert
        Assert.NotNull(capturedContent);
        Assert.DoesNotContain("retry=", capturedContent);
        Assert.DoesNotContain("expire=", capturedContent);
    }

    [Fact]
    public async Task SendNotification_WithSound_IncludesSound()
    {
        // Arrange
        var proxy = CreateProxy();
        string? capturedContent = null;

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
                capturedContent = await req.Content!.ReadAsStringAsync())
            .ReturnsAsync(CreateSuccessResponse());

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
        Assert.NotNull(capturedContent);
        Assert.Contains("sound=cosmic", capturedContent);
    }

    [Fact]
    public async Task SendNotification_WithDevice_IncludesDevice()
    {
        // Arrange
        var proxy = CreateProxy();
        string? capturedContent = null;

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
                capturedContent = await req.Content!.ReadAsStringAsync())
            .ReturnsAsync(CreateSuccessResponse());

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
        Assert.NotNull(capturedContent);
        Assert.Contains("device=my-phone", capturedContent);
    }

    [Fact]
    public async Task SendNotification_WithTags_IncludesTags()
    {
        // Arrange
        var proxy = CreateProxy();
        string? capturedContent = null;

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
                capturedContent = await req.Content!.ReadAsStringAsync())
            .ReturnsAsync(CreateSuccessResponse());

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
        Assert.NotNull(capturedContent);
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
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":0,\"errors\":[\"user key is invalid\"]}")
            });

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
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<PushoverException>(() =>
            proxy.SendNotification(CreatePayload()));
        Assert.Contains("Unable to connect to Pushover API", ex.Message);
    }

    #endregion

    #region Helper Methods

    private void SetupSuccessResponse()
    {
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(CreateSuccessResponse());
    }

    private static HttpResponseMessage CreateSuccessResponse()
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"status\":1,\"request\":\"abc123\"}")
        };
    }

    private void SetupErrorResponse(HttpStatusCode statusCode, string responseBody)
    {
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody)
            });
    }

    #endregion
}
