using System.Net;
using Cleanuparr.Infrastructure.Features.Notifications.Telegram;
using Cleanuparr.Shared.Helpers;
using Moq;
using Moq.Protected;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications.Telegram;

public class TelegramProxyTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;

    public TelegramProxyTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _httpClientFactoryMock
            .Setup(f => f.CreateClient(Constants.HttpClientWithRetryName))
            .Returns(httpClient);
    }

    private TelegramProxy CreateProxy()
    {
        return new TelegramProxy(_httpClientFactoryMock.Object);
    }

    private static TelegramPayload CreatePayload(string text = "Test message", string? photoUrl = null)
    {
        return new TelegramPayload
        {
            ChatId = "123456789",
            Text = text,
            PhotoUrl = photoUrl,
            MessageThreadId = null,
            DisableNotification = false
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
        await proxy.SendNotification(CreatePayload(), "test-bot-token");
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
        await proxy.SendNotification(CreatePayload(), "test-bot-token");

        // Assert
        Assert.Equal(HttpMethod.Post, capturedMethod);
    }

    [Fact]
    public async Task SendNotification_WithoutPhoto_UseSendMessageEndpoint()
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

        // Act
        await proxy.SendNotification(CreatePayload(), "my-bot-token");

        // Assert
        Assert.NotNull(capturedUri);
        Assert.Contains("/botmy-bot-token/sendMessage", capturedUri.ToString());
    }

    [Fact]
    public async Task SendNotification_WithPhotoAndShortCaption_UsesSendPhotoEndpoint()
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

        var payload = CreatePayload(text: "Short caption", photoUrl: "https://example.com/image.jpg");

        // Act
        await proxy.SendNotification(payload, "my-bot-token");

        // Assert
        Assert.NotNull(capturedUri);
        Assert.Contains("/botmy-bot-token/sendPhoto", capturedUri.ToString());
    }

    [Fact]
    public async Task SendNotification_WithPhotoAndLongCaption_UsesSendMessageEndpoint()
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

        // Caption longer than 1024 characters
        var payload = CreatePayload(text: new string('A', 1025), photoUrl: "https://example.com/image.jpg");

        // Act
        await proxy.SendNotification(payload, "my-bot-token");

        // Assert
        Assert.NotNull(capturedUri);
        Assert.Contains("/botmy-bot-token/sendMessage", capturedUri.ToString());
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
        await proxy.SendNotification(CreatePayload(), "test-bot-token");

        // Assert
        Assert.Equal("application/json", capturedContentType);
    }

    [Fact]
    public async Task SendNotification_WithPhotoAndLongCaption_IncludesInvisibleImageLink()
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
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        // Caption longer than 1024 characters
        var payload = CreatePayload(text: new string('A', 1025), photoUrl: "https://example.com/image.jpg");

        // Act
        await proxy.SendNotification(payload, "test-bot-token");

        // Assert
        Assert.NotNull(capturedContent);
        Assert.Contains("&#8203;", capturedContent); // Zero-width space
        Assert.Contains("example.com/image.jpg", capturedContent);
    }

    [Fact]
    public async Task SendNotification_WithoutPhoto_DisablesWebPagePreview()
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
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        // Act
        await proxy.SendNotification(CreatePayload(), "test-bot-token");

        // Assert
        Assert.NotNull(capturedContent);
        Assert.Contains("disable_web_page_preview", capturedContent);
        Assert.Contains("true", capturedContent);
    }

    [Fact]
    public async Task SendNotification_WithMessageThreadId_IncludesThreadIdInBody()
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
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var payload = new TelegramPayload
        {
            ChatId = "123456789",
            Text = "Test message",
            MessageThreadId = 42
        };

        // Act
        await proxy.SendNotification(payload, "test-bot-token");

        // Assert
        Assert.NotNull(capturedContent);
        Assert.Contains("message_thread_id", capturedContent);
        Assert.Contains("42", capturedContent);
    }

    [Fact]
    public async Task SendNotification_WithDisableNotification_IncludesDisableNotificationInBody()
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
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var payload = new TelegramPayload
        {
            ChatId = "123456789",
            Text = "Test message",
            DisableNotification = true
        };

        // Act
        await proxy.SendNotification(payload, "test-bot-token");

        // Assert
        Assert.NotNull(capturedContent);
        Assert.Contains("disable_notification", capturedContent);
    }

    #endregion

    #region SendNotification Error Tests

    [Fact]
    public async Task SendNotification_When400_ThrowsTelegramExceptionWithRejectedMessage()
    {
        // Arrange
        var proxy = CreateProxy();
        SetupErrorResponse(HttpStatusCode.BadRequest, "Bad Request: chat not found");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<TelegramException>(() =>
            proxy.SendNotification(CreatePayload(), "test-bot-token"));
        Assert.Contains("rejected the request", ex.Message);
    }

    [Fact]
    public async Task SendNotification_When401_ThrowsTelegramExceptionWithInvalidToken()
    {
        // Arrange
        var proxy = CreateProxy();
        SetupErrorResponse(HttpStatusCode.Unauthorized, "Unauthorized");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<TelegramException>(() =>
            proxy.SendNotification(CreatePayload(), "test-bot-token"));
        Assert.Contains("bot token is invalid", ex.Message);
    }

    [Fact]
    public async Task SendNotification_When403_ThrowsTelegramExceptionWithPermissionDenied()
    {
        // Arrange
        var proxy = CreateProxy();
        SetupErrorResponse(HttpStatusCode.Forbidden, "Forbidden: bot was blocked by the user");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<TelegramException>(() =>
            proxy.SendNotification(CreatePayload(), "test-bot-token"));
        Assert.Contains("permission", ex.Message);
    }

    [Fact]
    public async Task SendNotification_When429_ThrowsTelegramExceptionWithRateLimitMessage()
    {
        // Arrange
        var proxy = CreateProxy();
        SetupErrorResponse((HttpStatusCode)429, "Too Many Requests");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<TelegramException>(() =>
            proxy.SendNotification(CreatePayload(), "test-bot-token"));
        Assert.Contains("Rate limited", ex.Message);
    }

    [Fact]
    public async Task SendNotification_WhenOtherHttpError_ThrowsTelegramExceptionWithStatusCode()
    {
        // Arrange
        var proxy = CreateProxy();
        SetupErrorResponse(HttpStatusCode.InternalServerError, "Internal Server Error");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<TelegramException>(() =>
            proxy.SendNotification(CreatePayload(), "test-bot-token"));
        Assert.Contains("500", ex.Message);
    }

    [Fact]
    public async Task SendNotification_WhenNetworkError_ThrowsTelegramException()
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
        var ex = await Assert.ThrowsAsync<TelegramException>(() =>
            proxy.SendNotification(CreatePayload(), "test-bot-token"));
        Assert.Contains("Unable to reach Telegram API", ex.Message);
    }

    [Fact]
    public async Task SendNotification_WhenErrorResponseTruncatesLongBody()
    {
        // Arrange
        var proxy = CreateProxy();
        var longErrorBody = new string('X', 600);
        SetupErrorResponse(HttpStatusCode.BadRequest, longErrorBody);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<TelegramException>(() =>
            proxy.SendNotification(CreatePayload(), "test-bot-token"));
        Assert.True(ex.Message.Length < 600);
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

    private void SetupErrorResponse(HttpStatusCode statusCode, string body = "")
    {
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body)
            });
    }

    #endregion
}
