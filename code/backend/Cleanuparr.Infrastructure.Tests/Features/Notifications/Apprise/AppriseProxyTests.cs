using System.Net;
using Cleanuparr.Infrastructure.Features.Notifications.Apprise;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Cleanuparr.Shared.Helpers;
using Moq;
using Moq.Protected;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications.Apprise;

public class AppriseProxyTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;

    public AppriseProxyTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _httpClientFactoryMock
            .Setup(f => f.CreateClient(Constants.HttpClientWithRetryName))
            .Returns(httpClient);
    }

    private AppriseProxy CreateProxy()
    {
        return new AppriseProxy(_httpClientFactoryMock.Object);
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

        var config = new AppriseConfig { Url = "http://apprise.local", Key = "my-key" };

        // Act
        await proxy.SendNotification(CreatePayload(), config);

        // Assert
        Assert.NotNull(capturedUri);
        Assert.Contains("/notify/my-key", capturedUri.ToString());
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
    public async Task SendNotification_WithBasicAuth_SetsAuthorizationHeader()
    {
        // Arrange
        var proxy = CreateProxy();
        string? capturedAuthHeader = null;

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
                capturedAuthHeader = req.Headers.Authorization?.Scheme)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var config = new AppriseConfig { Url = "http://user:pass@apprise.local", Key = "test-key" };

        // Act
        await proxy.SendNotification(CreatePayload(), config);

        // Assert
        Assert.Equal("Basic", capturedAuthHeader);
    }

    #endregion

    #region SendNotification Error Tests

    [Fact]
    public async Task SendNotification_When401_ThrowsAppriseExceptionWithInvalidApiKey()
    {
        // Arrange
        var proxy = CreateProxy();
        SetupErrorResponse(HttpStatusCode.Unauthorized);

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
        SetupErrorResponse((HttpStatusCode)424);

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
        SetupErrorResponse(statusCode);

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
        SetupErrorResponse(HttpStatusCode.InternalServerError);

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
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AppriseException>(() =>
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
