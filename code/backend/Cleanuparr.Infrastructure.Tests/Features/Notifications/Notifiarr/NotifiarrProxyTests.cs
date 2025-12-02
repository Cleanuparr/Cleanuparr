using System.Net;
using Cleanuparr.Infrastructure.Features.Notifications.Notifiarr;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Cleanuparr.Shared.Helpers;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications.Notifiarr;

public class NotifiarrProxyTests
{
    private readonly Mock<ILogger<NotifiarrProxy>> _loggerMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;

    public NotifiarrProxyTests()
    {
        _loggerMock = new Mock<ILogger<NotifiarrProxy>>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _httpClientFactoryMock
            .Setup(f => f.CreateClient(Constants.HttpClientWithRetryName))
            .Returns(httpClient);
    }

    private NotifiarrProxy CreateProxy()
    {
        return new NotifiarrProxy(_loggerMock.Object, _httpClientFactoryMock.Object);
    }

    private static NotifiarrPayload CreatePayload()
    {
        return new NotifiarrPayload
        {
            Notification = new NotifiarrNotification { Update = false },
            Discord = new Discord
            {
                Color = "#FF0000",
                Text = new Text { Title = "Test", Content = "Test content" },
                Ids = new Ids { Channel = "123456789" }
            }
        };
    }

    private static NotifiarrConfig CreateConfig()
    {
        return new NotifiarrConfig
        {
            ApiKey = "test-api-key-12345",
            ChannelId = "123456789"
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

        var config = new NotifiarrConfig { ApiKey = "my-api-key", ChannelId = "123" };

        // Act
        await proxy.SendNotification(CreatePayload(), config);

        // Assert
        Assert.NotNull(capturedUri);
        Assert.Contains("notifiarr.com", capturedUri.ToString());
        Assert.Contains("passthrough", capturedUri.ToString());
        Assert.Contains("my-api-key", capturedUri.ToString());
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

    [Fact]
    public async Task SendNotification_When401_ThrowsNotifiarrExceptionWithInvalidApiKey()
    {
        // Arrange
        var proxy = CreateProxy();
        SetupErrorResponse(HttpStatusCode.Unauthorized);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotifiarrException>(() =>
            proxy.SendNotification(CreatePayload(), CreateConfig()));
        Assert.Contains("API key is invalid", ex.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task SendNotification_WhenServiceUnavailable_ThrowsNotifiarrException(HttpStatusCode statusCode)
    {
        // Arrange
        var proxy = CreateProxy();
        SetupErrorResponse(statusCode);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotifiarrException>(() =>
            proxy.SendNotification(CreatePayload(), CreateConfig()));
        Assert.Contains("service unavailable", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendNotification_WhenOtherError_ThrowsNotifiarrException()
    {
        // Arrange
        var proxy = CreateProxy();
        SetupErrorResponse(HttpStatusCode.InternalServerError);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotifiarrException>(() =>
            proxy.SendNotification(CreatePayload(), CreateConfig()));
        Assert.Contains("unable to send notification", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendNotification_WhenNetworkError_ThrowsNotifiarrException()
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
        var ex = await Assert.ThrowsAsync<NotifiarrException>(() =>
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
