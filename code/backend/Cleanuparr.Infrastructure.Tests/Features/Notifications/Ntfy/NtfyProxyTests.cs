using System.Net;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications.Ntfy;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Cleanuparr.Shared.Helpers;
using Moq;
using Moq.Protected;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications.Ntfy;

public class NtfyProxyTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;

    public NtfyProxyTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _httpClientFactoryMock
            .Setup(f => f.CreateClient(Constants.HttpClientWithRetryName))
            .Returns(httpClient);
    }

    private NtfyProxy CreateProxy()
    {
        return new NtfyProxy(_httpClientFactoryMock.Object);
    }

    private static NtfyPayload CreatePayload()
    {
        return new NtfyPayload
        {
            Topic = "test-topic",
            Message = "Test message",
            Title = "Test Title"
        };
    }

    private static NtfyConfig CreateConfig(NtfyAuthenticationType authType = NtfyAuthenticationType.None)
    {
        return new NtfyConfig
        {
            ServerUrl = "http://ntfy.local",
            Topics = new List<string> { "test-topic" },
            AuthenticationType = authType,
            Username = authType == NtfyAuthenticationType.BasicAuth ? "user" : null,
            Password = authType == NtfyAuthenticationType.BasicAuth ? "pass" : null,
            AccessToken = authType == NtfyAuthenticationType.AccessToken ? "token123" : null
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
        await proxy.SendNotification(CreatePayload(), CreateConfig());
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
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        // Act
        await proxy.SendNotification(CreatePayload(), CreateConfig());

        // Assert
        Assert.Equal(HttpMethod.Post, capturedMethod);
    }

    [Fact]
    public async Task SendNotification_SetsJsonContentType()
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
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        // Act
        await proxy.SendNotification(CreatePayload(), CreateConfig());

        // Assert
        Assert.Equal("application/json", capturedContentType);
    }

    #endregion

    #region Authentication Tests

    [Fact]
    public async Task SendNotification_WithNoAuth_DoesNotSetAuthorizationHeader()
    {
        // Arrange
        var proxy = CreateProxy();
        bool hasAuthHeader = false;

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
                hasAuthHeader = req.Headers.Authorization != null)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        // Act
        await proxy.SendNotification(CreatePayload(), CreateConfig(NtfyAuthenticationType.None));

        // Assert
        Assert.False(hasAuthHeader);
    }

    [Fact]
    public async Task SendNotification_WithBasicAuth_SetsBasicAuthorizationHeader()
    {
        // Arrange
        var proxy = CreateProxy();
        string? capturedAuthScheme = null;

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
                capturedAuthScheme = req.Headers.Authorization?.Scheme)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        // Act
        await proxy.SendNotification(CreatePayload(), CreateConfig(NtfyAuthenticationType.BasicAuth));

        // Assert
        Assert.Equal("Basic", capturedAuthScheme);
    }

    [Fact]
    public async Task SendNotification_WithAccessToken_SetsBearerAuthorizationHeader()
    {
        // Arrange
        var proxy = CreateProxy();
        string? capturedAuthScheme = null;

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
                capturedAuthScheme = req.Headers.Authorization?.Scheme)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        // Act
        await proxy.SendNotification(CreatePayload(), CreateConfig(NtfyAuthenticationType.AccessToken));

        // Assert
        Assert.Equal("Bearer", capturedAuthScheme);
    }

    #endregion

    #region SendNotification Error Tests

    [Fact]
    public async Task SendNotification_When400_ThrowsNtfyExceptionWithBadRequest()
    {
        // Arrange
        var proxy = CreateProxy();
        SetupErrorResponse(HttpStatusCode.BadRequest);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NtfyException>(() =>
            proxy.SendNotification(CreatePayload(), CreateConfig()));
        Assert.Contains("Bad request", ex.Message);
    }

    [Fact]
    public async Task SendNotification_When401_ThrowsNtfyExceptionWithUnauthorized()
    {
        // Arrange
        var proxy = CreateProxy();
        SetupErrorResponse(HttpStatusCode.Unauthorized);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NtfyException>(() =>
            proxy.SendNotification(CreatePayload(), CreateConfig()));
        Assert.Contains("Unauthorized", ex.Message);
    }

    [Fact]
    public async Task SendNotification_When413_ThrowsNtfyExceptionWithPayloadTooLarge()
    {
        // Arrange
        var proxy = CreateProxy();
        SetupErrorResponse(HttpStatusCode.RequestEntityTooLarge);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NtfyException>(() =>
            proxy.SendNotification(CreatePayload(), CreateConfig()));
        Assert.Contains("Payload too large", ex.Message);
    }

    [Fact]
    public async Task SendNotification_When429_ThrowsNtfyExceptionWithRateLimited()
    {
        // Arrange
        var proxy = CreateProxy();
        SetupErrorResponse(HttpStatusCode.TooManyRequests);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NtfyException>(() =>
            proxy.SendNotification(CreatePayload(), CreateConfig()));
        Assert.Contains("Rate limited", ex.Message);
    }

    [Fact]
    public async Task SendNotification_When507_ThrowsNtfyExceptionWithInsufficientStorage()
    {
        // Arrange
        var proxy = CreateProxy();
        SetupErrorResponse(HttpStatusCode.InsufficientStorage);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NtfyException>(() =>
            proxy.SendNotification(CreatePayload(), CreateConfig()));
        Assert.Contains("Insufficient storage", ex.Message);
    }

    [Fact]
    public async Task SendNotification_WhenOtherError_ThrowsNtfyException()
    {
        // Arrange
        var proxy = CreateProxy();
        SetupErrorResponse(HttpStatusCode.InternalServerError);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NtfyException>(() =>
            proxy.SendNotification(CreatePayload(), CreateConfig()));
        Assert.Contains("Unable to send notification", ex.Message);
    }

    [Fact]
    public async Task SendNotification_WhenNetworkError_ThrowsNtfyException()
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
        var ex = await Assert.ThrowsAsync<NtfyException>(() =>
            proxy.SendNotification(CreatePayload(), CreateConfig()));
        Assert.Contains("Unable to send notification", ex.Message);
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
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));
    }

    private void SetupErrorResponse(HttpStatusCode statusCode)
    {
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Error", null, statusCode));
    }

    #endregion
}
