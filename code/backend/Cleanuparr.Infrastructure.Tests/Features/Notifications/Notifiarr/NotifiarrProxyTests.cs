using System.Net;
using Cleanuparr.Infrastructure.Features.Notifications.Notifiarr;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Cleanuparr.Shared.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications.Notifiarr;

public class NotifiarrProxyTests
{
    private readonly ILogger<NotifiarrProxy> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly FakeHttpMessageHandler _httpMessageHandler;

    public NotifiarrProxyTests()
    {
        _logger = Substitute.For<ILogger<NotifiarrProxy>>();
        _httpMessageHandler = new FakeHttpMessageHandler();
        _httpClientFactory = Substitute.For<IHttpClientFactory>();

        var httpClient = new HttpClient(_httpMessageHandler);
        _httpClientFactory
            .CreateClient(Constants.HttpClientWithRetryName)
            .Returns(httpClient);
    }

    private NotifiarrProxy CreateProxy()
    {
        return new NotifiarrProxy(_logger, _httpClientFactory);
    }

    private static NotifiarrPayload CreatePayload()
    {
        return new NotifiarrPayload
        {
            Notification = new NotifiarrNotification { Update = false },
            Discord = new NotifiarrDiscord
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
        await proxy.SendNotification(CreatePayload(), CreateConfig());
    }

    [Fact]
    public async Task SendNotification_SendsPostRequest()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupResponse(HttpStatusCode.OK);

        // Act
        await proxy.SendNotification(CreatePayload(), CreateConfig());

        // Assert
        _httpMessageHandler.CapturedRequests[0].Method.ShouldBe(HttpMethod.Post);
    }

    [Fact]
    public async Task SendNotification_BuildsCorrectUrl()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupResponse(HttpStatusCode.OK);

        var config = new NotifiarrConfig { ApiKey = "my-api-key", ChannelId = "123" };

        // Act
        await proxy.SendNotification(CreatePayload(), config);

        // Assert
        var capturedUri = _httpMessageHandler.CapturedRequests[0].RequestUri?.ToString();
        capturedUri.ShouldNotBeNull();
        capturedUri.ShouldContain("notifiarr.com");
        capturedUri.ShouldContain("passthrough");
        capturedUri.ShouldContain("my-api-key");
    }

    [Fact]
    public async Task SendNotification_SetsJsonContentType()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupResponse(HttpStatusCode.OK);

        // Act
        await proxy.SendNotification(CreatePayload(), CreateConfig());

        // Assert
        _httpMessageHandler.CapturedRequests[0].Content?.Headers.ContentType?.MediaType.ShouldBe("application/json");
    }

    [Fact]
    public async Task SendNotification_LogsTraceWithContent()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupResponse(HttpStatusCode.OK);

        // Act
        await proxy.SendNotification(CreatePayload(), CreateConfig());

        // Assert
        _logger.ReceivedLogContaining(LogLevel.Trace, "sending notification");
    }

    #endregion

    #region SendNotification Error Tests

    [Fact]
    public async Task SendNotification_When401_ThrowsNotifiarrExceptionWithInvalidApiKey()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupThrow(new HttpRequestException("Error", null, HttpStatusCode.Unauthorized));

        // Act & Assert
        var ex = await Should.ThrowAsync<NotifiarrException>(() =>
            proxy.SendNotification(CreatePayload(), CreateConfig()));
        ex.Message.ShouldContain("API key is invalid");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task SendNotification_WhenServiceUnavailable_ThrowsNotifiarrException(HttpStatusCode statusCode)
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupThrow(new HttpRequestException("Error", null, statusCode));

        // Act & Assert
        var ex = await Should.ThrowAsync<NotifiarrException>(() =>
            proxy.SendNotification(CreatePayload(), CreateConfig()));
        ex.Message.ShouldContain("service unavailable", Case.Insensitive);
    }

    [Fact]
    public async Task SendNotification_WhenOtherError_ThrowsNotifiarrException()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupThrow(new HttpRequestException("Error", null, HttpStatusCode.InternalServerError));

        // Act & Assert
        var ex = await Should.ThrowAsync<NotifiarrException>(() =>
            proxy.SendNotification(CreatePayload(), CreateConfig()));
        ex.Message.ShouldContain("unable to send notification", Case.Insensitive);
    }

    [Fact]
    public async Task SendNotification_WhenNetworkError_ThrowsNotifiarrException()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupThrow(new HttpRequestException("Network error"));

        // Act & Assert
        var ex = await Should.ThrowAsync<NotifiarrException>(() =>
            proxy.SendNotification(CreatePayload(), CreateConfig()));
        ex.Message.ShouldContain("unable to send notification", Case.Insensitive);
    }

    #endregion
}
