using System.Net;
using Cleanuparr.Infrastructure.Features.Notifications.Apprise;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Cleanuparr.Shared.Helpers;
using NSubstitute;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications.Apprise;

public class AppriseProxyTests
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly FakeHttpMessageHandler _httpMessageHandler;

    public AppriseProxyTests()
    {
        _httpMessageHandler = new FakeHttpMessageHandler();
        _httpClientFactory = Substitute.For<IHttpClientFactory>();

        var httpClient = new HttpClient(_httpMessageHandler);
        _httpClientFactory
            .CreateClient(Constants.HttpClientWithRetryName)
            .Returns(httpClient);
    }

    private AppriseProxy CreateProxy()
    {
        return new AppriseProxy(_httpClientFactory);
    }

    private static ApprisePayload CreatePayload()
    {
        return new ApprisePayload
        {
            Title = "Test Title",
            Body = "Test Body"
        };
    }

    private static AppriseConfig CreateConfig()
    {
        return new AppriseConfig
        {
            Url = "http://apprise.local",
            Key = "test-key"
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

        var config = new AppriseConfig { Url = "http://apprise.local", Key = "my-key" };

        // Act
        await proxy.SendNotification(CreatePayload(), config);

        // Assert
        Assert.Contains("/notify/my-key", _httpMessageHandler.CapturedRequests[0].RequestUri?.ToString());
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
    public async Task SendNotification_WithBasicAuth_SetsAuthorizationHeader()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupResponse(HttpStatusCode.OK);

        var config = new AppriseConfig { Url = "http://user:pass@apprise.local", Key = "test-key" };

        // Act
        await proxy.SendNotification(CreatePayload(), config);

        // Assert
        Assert.Equal("Basic", _httpMessageHandler.CapturedRequests[0].Headers.Authorization?.Scheme);
    }

    #endregion

    #region SendNotification Error Tests

    [Fact]
    public async Task SendNotification_When401_ThrowsAppriseExceptionWithInvalidApiKey()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupThrow(new HttpRequestException("Error", null, HttpStatusCode.Unauthorized));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AppriseException>(() =>
            proxy.SendNotification(CreatePayload(), CreateConfig()));
        Assert.Contains("API key is invalid", ex.Message);
    }

    [Fact]
    public async Task SendNotification_When424_ThrowsAppriseExceptionWithTagsError()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupThrow(new HttpRequestException("Error", null, (HttpStatusCode)424));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AppriseException>(() =>
            proxy.SendNotification(CreatePayload(), CreateConfig()));
        Assert.Contains("tags", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task SendNotification_WhenServiceUnavailable_ThrowsAppriseException(HttpStatusCode statusCode)
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupThrow(new HttpRequestException("Error", null, statusCode));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AppriseException>(() =>
            proxy.SendNotification(CreatePayload(), CreateConfig()));
        Assert.Contains("service unavailable", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendNotification_WhenOtherError_ThrowsAppriseException()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupThrow(new HttpRequestException("Error", null, HttpStatusCode.InternalServerError));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AppriseException>(() =>
            proxy.SendNotification(CreatePayload(), CreateConfig()));
        Assert.Contains("Unable to send notification", ex.Message);
    }

    [Fact]
    public async Task SendNotification_WhenNetworkError_ThrowsAppriseException()
    {
        // Arrange
        var proxy = CreateProxy();
        _httpMessageHandler.SetupThrow(new HttpRequestException("Network error"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AppriseException>(() =>
            proxy.SendNotification(CreatePayload(), CreateConfig()));
        Assert.Contains("Unable to send notification", ex.Message);
    }

    #endregion
}
