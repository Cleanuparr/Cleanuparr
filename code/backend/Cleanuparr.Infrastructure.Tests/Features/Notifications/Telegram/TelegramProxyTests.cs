using System.Net;
using Cleanuparr.Infrastructure.Features.Notifications.Telegram;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Shared.Helpers;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications.Telegram;

public class TelegramProxyTests
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly FakeHttpMessageHandler _httpMessageHandler;

    public TelegramProxyTests()
    {
        _httpMessageHandler = new FakeHttpMessageHandler();
        _httpClientFactory = Substitute.For<IHttpClientFactory>();

        var httpClient = new HttpClient(_httpMessageHandler);
        _httpClientFactory
            .CreateClient(Constants.HttpClientWithRetryName)
            .Returns(httpClient);
    }

    private TelegramProxy CreateProxy()
    {
        return new TelegramProxy(_httpClientFactory);
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
        _httpMessageHandler.SetupResponse(HttpStatusCode.OK);

        // Act & Assert - Should not throw
        await proxy.SendNotification(CreatePayload(), "test-bot-token");
    }

    [Fact]
    public async Task SendNotification_SendsPostRequest()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupResponse(HttpStatusCode.OK);

        // Act
        await proxy.SendNotification(CreatePayload(), "test-bot-token");

        // Assert
        _httpMessageHandler.CapturedRequests[0].Method.ShouldBe(HttpMethod.Post);
    }

    [Fact]
    public async Task SendNotification_WithoutPhoto_UseSendMessageEndpoint()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupResponse(HttpStatusCode.OK);

        // Act
        await proxy.SendNotification(CreatePayload(), "my-bot-token");

        // Assert
        _httpMessageHandler.CapturedRequests[0].RequestUri?.ToString().ShouldContain("/botmy-bot-token/sendMessage");
    }

    [Fact]
    public async Task SendNotification_WithPhotoAndShortCaption_UsesSendPhotoEndpoint()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupResponse(HttpStatusCode.OK);

        var payload = CreatePayload(text: "Short caption", photoUrl: "https://example.com/image.jpg");

        // Act
        await proxy.SendNotification(payload, "my-bot-token");

        // Assert
        _httpMessageHandler.CapturedRequests[0].RequestUri?.ToString().ShouldContain("/botmy-bot-token/sendPhoto");
    }

    [Fact]
    public async Task SendNotification_WithPhotoAndLongCaption_UsesSendMessageEndpoint()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupResponse(HttpStatusCode.OK);

        // Caption longer than 1024 characters
        var payload = CreatePayload(text: new string('A', 1025), photoUrl: "https://example.com/image.jpg");

        // Act
        await proxy.SendNotification(payload, "my-bot-token");

        // Assert
        _httpMessageHandler.CapturedRequests[0].RequestUri?.ToString().ShouldContain("/botmy-bot-token/sendMessage");
    }

    [Fact]
    public async Task SendNotification_SetsJsonContentType()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupResponse(HttpStatusCode.OK);

        // Act
        await proxy.SendNotification(CreatePayload(), "test-bot-token");

        // Assert
        _httpMessageHandler.CapturedRequests[0].Content?.Headers.ContentType?.MediaType.ShouldBe("application/json");
    }

    [Fact]
    public async Task SendNotification_WithPhotoAndLongCaption_IncludesInvisibleImageLink()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupResponse(HttpStatusCode.OK);

        // Caption longer than 1024 characters
        var payload = CreatePayload(text: new string('A', 1025), photoUrl: "https://example.com/image.jpg");

        // Act
        await proxy.SendNotification(payload, "test-bot-token");

        // Assert
        var capturedContent = _httpMessageHandler.CapturedRequestBodies[0]!;
        capturedContent.ShouldContain("&#8203;"); // Zero-width space
        capturedContent.ShouldContain("example.com/image.jpg");
    }

    [Fact]
    public async Task SendNotification_WithoutPhoto_DisablesWebPagePreview()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupResponse(HttpStatusCode.OK);

        // Act
        await proxy.SendNotification(CreatePayload(), "test-bot-token");

        // Assert
        var capturedContent = _httpMessageHandler.CapturedRequestBodies[0]!;
        capturedContent.ShouldContain("disable_web_page_preview");
        capturedContent.ShouldContain("true");
    }

    [Fact]
    public async Task SendNotification_WithMessageThreadId_IncludesThreadIdInBody()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupResponse(HttpStatusCode.OK);

        var payload = new TelegramPayload
        {
            ChatId = "123456789",
            Text = "Test message",
            MessageThreadId = 42
        };

        // Act
        await proxy.SendNotification(payload, "test-bot-token");

        // Assert
        var capturedContent = _httpMessageHandler.CapturedRequestBodies[0]!;
        capturedContent.ShouldContain("message_thread_id");
        capturedContent.ShouldContain("42");
    }

    [Fact]
    public async Task SendNotification_WithDisableNotification_IncludesDisableNotificationInBody()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupResponse(HttpStatusCode.OK);

        var payload = new TelegramPayload
        {
            ChatId = "123456789",
            Text = "Test message",
            DisableNotification = true
        };

        // Act
        await proxy.SendNotification(payload, "test-bot-token");

        // Assert
        var capturedContent = _httpMessageHandler.CapturedRequestBodies[0]!;
        capturedContent.ShouldContain("disable_notification");
    }

    #endregion

    #region SendNotification Error Tests

    [Fact]
    public async Task SendNotification_When400_ThrowsTelegramExceptionWithRejectedMessage()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("Bad Request: chat not found")
        }));

        // Act & Assert
        var ex = await Should.ThrowAsync<TelegramException>(() =>
            proxy.SendNotification(CreatePayload(), "test-bot-token"));
        ex.Message.ShouldContain("rejected the request");
    }

    [Fact]
    public async Task SendNotification_When401_ThrowsTelegramExceptionWithInvalidToken()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("Unauthorized")
        }));

        // Act & Assert
        var ex = await Should.ThrowAsync<TelegramException>(() =>
            proxy.SendNotification(CreatePayload(), "test-bot-token"));
        ex.Message.ShouldContain("bot token is invalid");
    }

    [Fact]
    public async Task SendNotification_When403_ThrowsTelegramExceptionWithPermissionDenied()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("Forbidden: bot was blocked by the user")
        }));

        // Act & Assert
        var ex = await Should.ThrowAsync<TelegramException>(() =>
            proxy.SendNotification(CreatePayload(), "test-bot-token"));
        ex.Message.ShouldContain("permission");
    }

    [Fact]
    public async Task SendNotification_When429_ThrowsTelegramExceptionWithRateLimitMessage()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)429)
        {
            Content = new StringContent("Too Many Requests")
        }));

        // Act & Assert
        var ex = await Should.ThrowAsync<TelegramException>(() =>
            proxy.SendNotification(CreatePayload(), "test-bot-token"));
        ex.Message.ShouldContain("Rate limited");
    }

    [Fact]
    public async Task SendNotification_WhenOtherHttpError_ThrowsTelegramExceptionWithStatusCode()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("Internal Server Error")
        }));

        // Act & Assert
        var ex = await Should.ThrowAsync<TelegramException>(() =>
            proxy.SendNotification(CreatePayload(), "test-bot-token"));
        ex.Message.ShouldContain("500");
    }

    [Fact]
    public async Task SendNotification_WhenNetworkError_ThrowsTelegramException()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupThrow(new HttpRequestException("Network error"));

        // Act & Assert
        var ex = await Should.ThrowAsync<TelegramException>(() =>
            proxy.SendNotification(CreatePayload(), "test-bot-token"));
        ex.Message.ShouldContain("Unable to reach Telegram API");
    }

    [Fact]
    public async Task SendNotification_WhenErrorResponseTruncatesLongBody()
    {
        // Arrange
        var proxy = CreateProxy();
        var longErrorBody = new string('X', 600);
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(longErrorBody)
        }));

        // Act & Assert
        var ex = await Should.ThrowAsync<TelegramException>(() =>
            proxy.SendNotification(CreatePayload(), "test-bot-token"));
        (ex.Message.Length < 600).ShouldBeTrue();
    }

    #endregion
}
