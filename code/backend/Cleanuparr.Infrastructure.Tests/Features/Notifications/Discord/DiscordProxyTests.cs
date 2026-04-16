using System.Net;
using Cleanuparr.Infrastructure.Features.Notifications.Discord;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Cleanuparr.Shared.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications.Discord;

public class DiscordProxyTests
{
    private readonly ILogger<DiscordProxy> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly FakeHttpMessageHandler _httpMessageHandler;

    public DiscordProxyTests()
    {
        _logger = Substitute.For<ILogger<DiscordProxy>>();
        _httpMessageHandler = new FakeHttpMessageHandler();
        _httpClientFactory = Substitute.For<IHttpClientFactory>();

        var httpClient = new HttpClient(_httpMessageHandler);
        _httpClientFactory
            .CreateClient(Constants.HttpClientWithRetryName)
            .Returns(httpClient);
    }

    private DiscordProxy CreateProxy()
    {
        return new DiscordProxy(_logger, _httpClientFactory);
    }

    private static DiscordPayload CreatePayload()
    {
        return new DiscordPayload
        {
            Embeds = new List<DiscordEmbed>
            {
                new()
                {
                    Title = "Test Title",
                    Description = "Test Description",
                    Color = 0x28a745
                }
            }
        };
    }

    private static DiscordConfig CreateConfig()
    {
        return new DiscordConfig
        {
            WebhookUrl = "https://discord.com/api/webhooks/123456789/abcdefghij",
            Username = "Test Bot",
            AvatarUrl = "https://example.com/avatar.png"
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
        Assert.Equal(HttpMethod.Post, _httpMessageHandler.CapturedRequests[0].Method);
    }

    [Fact]
    public async Task SendNotification_BuildsCorrectUrl()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupResponse(HttpStatusCode.OK);

        var config = new DiscordConfig
        {
            WebhookUrl = "https://discord.com/api/webhooks/123/abc"
        };

        // Act
        await proxy.SendNotification(CreatePayload(), config);

        // Assert
        Assert.Equal("https://discord.com/api/webhooks/123/abc", _httpMessageHandler.CapturedRequests[0].RequestUri?.ToString());
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
        Assert.Equal("application/json", _httpMessageHandler.CapturedRequests[0].Content?.Headers.ContentType?.MediaType);
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

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task SendNotification_WhenUnauthorized_ThrowsDiscordExceptionWithInvalidWebhook(HttpStatusCode statusCode)
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupThrow(new HttpRequestException("Error", null, statusCode));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DiscordException>(() =>
            proxy.SendNotification(CreatePayload(), CreateConfig()));
        Assert.Contains("invalid or unauthorized", ex.Message);
    }

    [Fact]
    public async Task SendNotification_When404_ThrowsDiscordExceptionWithNotFound()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupThrow(new HttpRequestException("Error", null, HttpStatusCode.NotFound));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DiscordException>(() =>
            proxy.SendNotification(CreatePayload(), CreateConfig()));
        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public async Task SendNotification_When429_ThrowsDiscordExceptionWithRateLimited()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupThrow(new HttpRequestException("Error", null, (HttpStatusCode)429));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DiscordException>(() =>
            proxy.SendNotification(CreatePayload(), CreateConfig()));
        Assert.Contains("rate limited", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task SendNotification_WhenServiceUnavailable_ThrowsDiscordException(HttpStatusCode statusCode)
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupThrow(new HttpRequestException("Error", null, statusCode));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DiscordException>(() =>
            proxy.SendNotification(CreatePayload(), CreateConfig()));
        Assert.Contains("service unavailable", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendNotification_WhenOtherError_ThrowsDiscordException()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupThrow(new HttpRequestException("Error", null, HttpStatusCode.InternalServerError));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DiscordException>(() =>
            proxy.SendNotification(CreatePayload(), CreateConfig()));
        Assert.Contains("unable to send notification", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendNotification_WhenNetworkError_ThrowsDiscordException()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupThrow(new HttpRequestException("Network error"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DiscordException>(() =>
            proxy.SendNotification(CreatePayload(), CreateConfig()));
        Assert.Contains("unable to send notification", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
