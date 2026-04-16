using System.Net;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications.Ntfy;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Cleanuparr.Shared.Helpers;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications.Ntfy;

public class NtfyProxyTests
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly FakeHttpMessageHandler _httpMessageHandler;

    public NtfyProxyTests()
    {
        _httpMessageHandler = new FakeHttpMessageHandler();
        _httpClientFactory = Substitute.For<IHttpClientFactory>();

        var httpClient = new HttpClient(_httpMessageHandler);
        _httpClientFactory
            .CreateClient(Constants.HttpClientWithRetryName)
            .Returns(httpClient);
    }

    private NtfyProxy CreateProxy()
    {
        return new NtfyProxy(_httpClientFactory);
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

    #endregion

    #region Authentication Tests

    [Fact]
    public async Task SendNotification_WithNoAuth_DoesNotSetAuthorizationHeader()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupResponse(HttpStatusCode.OK);

        // Act
        await proxy.SendNotification(CreatePayload(), CreateConfig(NtfyAuthenticationType.None));

        // Assert
        _httpMessageHandler.CapturedRequests[0].Headers.Authorization.ShouldBeNull();
    }

    [Fact]
    public async Task SendNotification_WithBasicAuth_SetsBasicAuthorizationHeader()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupResponse(HttpStatusCode.OK);

        // Act
        await proxy.SendNotification(CreatePayload(), CreateConfig(NtfyAuthenticationType.BasicAuth));

        // Assert
        _httpMessageHandler.CapturedRequests[0].Headers.Authorization?.Scheme.ShouldBe("Basic");
    }

    [Fact]
    public async Task SendNotification_WithAccessToken_SetsBearerAuthorizationHeader()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupResponse(HttpStatusCode.OK);

        // Act
        await proxy.SendNotification(CreatePayload(), CreateConfig(NtfyAuthenticationType.AccessToken));

        // Assert
        _httpMessageHandler.CapturedRequests[0].Headers.Authorization?.Scheme.ShouldBe("Bearer");
    }

    #endregion

    #region SendNotification Error Tests

    [Fact]
    public async Task SendNotification_When400_ThrowsNtfyExceptionWithBadRequest()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupThrow(new HttpRequestException("Error", null, HttpStatusCode.BadRequest));

        // Act & Assert
        var ex = await Should.ThrowAsync<NtfyException>(() =>
            proxy.SendNotification(CreatePayload(), CreateConfig()));
        ex.Message.ShouldContain("Bad request");
    }

    [Fact]
    public async Task SendNotification_When401_ThrowsNtfyExceptionWithUnauthorized()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupThrow(new HttpRequestException("Error", null, HttpStatusCode.Unauthorized));

        // Act & Assert
        var ex = await Should.ThrowAsync<NtfyException>(() =>
            proxy.SendNotification(CreatePayload(), CreateConfig()));
        ex.Message.ShouldContain("Unauthorized");
    }

    [Fact]
    public async Task SendNotification_When413_ThrowsNtfyExceptionWithPayloadTooLarge()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupThrow(new HttpRequestException("Error", null, HttpStatusCode.RequestEntityTooLarge));

        // Act & Assert
        var ex = await Should.ThrowAsync<NtfyException>(() =>
            proxy.SendNotification(CreatePayload(), CreateConfig()));
        ex.Message.ShouldContain("Payload too large");
    }

    [Fact]
    public async Task SendNotification_When429_ThrowsNtfyExceptionWithRateLimited()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupThrow(new HttpRequestException("Error", null, HttpStatusCode.TooManyRequests));

        // Act & Assert
        var ex = await Should.ThrowAsync<NtfyException>(() =>
            proxy.SendNotification(CreatePayload(), CreateConfig()));
        ex.Message.ShouldContain("Rate limited");
    }

    [Fact]
    public async Task SendNotification_When507_ThrowsNtfyExceptionWithInsufficientStorage()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupThrow(new HttpRequestException("Error", null, HttpStatusCode.InsufficientStorage));

        // Act & Assert
        var ex = await Should.ThrowAsync<NtfyException>(() =>
            proxy.SendNotification(CreatePayload(), CreateConfig()));
        ex.Message.ShouldContain("Insufficient storage");
    }

    [Fact]
    public async Task SendNotification_WhenOtherError_ThrowsNtfyException()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupThrow(new HttpRequestException("Error", null, HttpStatusCode.InternalServerError));

        // Act & Assert
        var ex = await Should.ThrowAsync<NtfyException>(() =>
            proxy.SendNotification(CreatePayload(), CreateConfig()));
        ex.Message.ShouldContain("Unable to send notification");
    }

    [Fact]
    public async Task SendNotification_WhenNetworkError_ThrowsNtfyException()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupThrow(new HttpRequestException("Network error"));

        // Act & Assert
        var ex = await Should.ThrowAsync<NtfyException>(() =>
            proxy.SendNotification(CreatePayload(), CreateConfig()));
        ex.Message.ShouldContain("Unable to send notification");
    }

    #endregion
}
