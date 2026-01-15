using System.Net;
using Cleanuparr.Infrastructure.Features.Notifications.Gotify;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Cleanuparr.Shared.Helpers;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications.Gotify;

public class GotifyProxyTests
{
    private readonly Mock<ILogger<GotifyProxy>> _loggerMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;

    public GotifyProxyTests()
    {
        _loggerMock = new Mock<ILogger<GotifyProxy>>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _httpClientFactoryMock
            .Setup(f => f.CreateClient(Constants.HttpClientWithRetryName))
            .Returns(httpClient);
    }

    private GotifyProxy CreateProxy()
    {
        return new GotifyProxy(_loggerMock.Object, _httpClientFactoryMock.Object);
    }

    private static GotifyPayload CreatePayload()
    {
        return new GotifyPayload
        {
            Title = "Test Title",
            Message = "Test Message",
            Priority = 5
        };
    }

    private static GotifyConfig CreateConfig()
    {
        return new GotifyConfig
        {
            ServerUrl = "https://gotify.example.com",
            ApplicationToken = "test-app-token",
            Priority = 5
        };
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
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
    public async Task SendNotification_BuildsCorrectUrl()
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
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var config = new GotifyConfig
        {
            ServerUrl = "https://gotify.example.com",
            ApplicationToken = "my-token",
            Priority = 5
        };

        // Act
        await proxy.SendNotification(CreatePayload(), config);

        // Assert
        Assert.NotNull(capturedUri);
        Assert.Equal("https://gotify.example.com/message?token=my-token", capturedUri.ToString());
    }

    [Fact]
    public async Task SendNotification_TrimsTrailingSlashFromServerUrl()
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
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var config = new GotifyConfig
        {
            ServerUrl = "https://gotify.example.com/",
            ApplicationToken = "my-token",
            Priority = 5
        };

        // Act
        await proxy.SendNotification(CreatePayload(), config);

        // Assert
        Assert.NotNull(capturedUri);
        Assert.Equal("https://gotify.example.com/message?token=my-token", capturedUri.ToString());
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

    [Fact]
    public async Task SendNotification_LogsTraceWithContent()
    {
        // Arrange
        var proxy = CreateProxy();
        SetupSuccessResponse();

        // Act
        await proxy.SendNotification(CreatePayload(), CreateConfig());

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Trace,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("sending notification")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region SendNotification Error Tests

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task SendNotification_WhenUnauthorized_ThrowsGotifyExceptionWithInvalidToken(HttpStatusCode statusCode)
    {
        // Arrange
        var proxy = CreateProxy();
        SetupErrorResponse(statusCode);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<GotifyException>(() =>
            proxy.SendNotification(CreatePayload(), CreateConfig()));
        Assert.Contains("invalid or unauthorized", ex.Message);
    }

    [Fact]
    public async Task SendNotification_When404_ThrowsGotifyExceptionWithNotFound()
    {
        // Arrange
        var proxy = CreateProxy();
        SetupErrorResponse(HttpStatusCode.NotFound);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<GotifyException>(() =>
            proxy.SendNotification(CreatePayload(), CreateConfig()));
        Assert.Contains("not found", ex.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task SendNotification_WhenServiceUnavailable_ThrowsGotifyException(HttpStatusCode statusCode)
    {
        // Arrange
        var proxy = CreateProxy();
        SetupErrorResponse(statusCode);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<GotifyException>(() =>
            proxy.SendNotification(CreatePayload(), CreateConfig()));
        Assert.Contains("service unavailable", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendNotification_WhenOtherError_ThrowsGotifyException()
    {
        // Arrange
        var proxy = CreateProxy();
        SetupErrorResponse(HttpStatusCode.InternalServerError);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<GotifyException>(() =>
            proxy.SendNotification(CreatePayload(), CreateConfig()));
        Assert.Contains("unable to send notification", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendNotification_WhenNetworkError_ThrowsGotifyException()
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
        var ex = await Assert.ThrowsAsync<GotifyException>(() =>
            proxy.SendNotification(CreatePayload(), CreateConfig()));
        Assert.Contains("unable to send notification", ex.Message, StringComparison.OrdinalIgnoreCase);
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
