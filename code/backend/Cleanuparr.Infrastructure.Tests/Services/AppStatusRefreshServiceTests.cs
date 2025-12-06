using System.Net;
using System.Text;
using System.Text.Json;
using Cleanuparr.Domain.Entities.AppStatus;
using Cleanuparr.Infrastructure.Hubs;
using Cleanuparr.Infrastructure.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Services;

public class AppStatusRefreshServiceTests : IDisposable
{
    private readonly Mock<ILogger<AppStatusRefreshService>> _loggerMock;
    private readonly Mock<IHubContext<AppHub>> _hubContextMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly AppStatusSnapshot _snapshot;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Mock<HttpMessageHandler> _httpHandlerMock;
    private AppStatusRefreshService? _service;

    public AppStatusRefreshServiceTests()
    {
        _loggerMock = new Mock<ILogger<AppStatusRefreshService>>();
        _hubContextMock = new Mock<IHubContext<AppHub>>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _snapshot = new AppStatusSnapshot();
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        _httpHandlerMock = new Mock<HttpMessageHandler>();

        // Setup hub context
        var clientsMock = new Mock<IHubClients>();
        var clientProxyMock = new Mock<IClientProxy>();
        clientsMock.Setup(c => c.All).Returns(clientProxyMock.Object);
        _hubContextMock.Setup(h => h.Clients).Returns(clientsMock.Object);
    }

    public void Dispose()
    {
        _service?.Dispose();
    }

    private AppStatusRefreshService CreateService()
    {
        _service = new AppStatusRefreshService(
            _loggerMock.Object,
            _hubContextMock.Object,
            _httpClientFactoryMock.Object,
            _snapshot,
            _jsonOptions);
        return _service;
    }

    private void SetupHttpResponse(HttpStatusCode statusCode, string content)
    {
        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(_httpHandlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_SetsAllDependencies()
    {
        // Act
        var service = CreateService();

        // Assert
        Assert.NotNull(service);
    }

    #endregion

    #region AppStatusSnapshot Integration Tests

    [Fact]
    public void AppStatusSnapshot_UpdateLatestVersion_ChangesStatusReturnsTrue()
    {
        // Arrange
        var snapshot = new AppStatusSnapshot();

        // Act
        var result = snapshot.UpdateLatestVersion("1.0.0", out var status);

        // Assert
        Assert.True(result);
        Assert.Equal("1.0.0", status.LatestVersion);
    }

    [Fact]
    public void AppStatusSnapshot_UpdateLatestVersion_SameVersionReturnsFalse()
    {
        // Arrange
        var snapshot = new AppStatusSnapshot();
        snapshot.UpdateLatestVersion("1.0.0", out _);

        // Act
        var result = snapshot.UpdateLatestVersion("1.0.0", out var status);

        // Assert
        Assert.False(result);
        Assert.Equal("1.0.0", status.LatestVersion);
    }

    [Fact]
    public void AppStatusSnapshot_UpdateCurrentVersion_ChangesStatusReturnsTrue()
    {
        // Arrange
        var snapshot = new AppStatusSnapshot();

        // Act
        var result = snapshot.UpdateCurrentVersion("2.0.0", out var status);

        // Assert
        Assert.True(result);
        Assert.Equal("2.0.0", status.CurrentVersion);
    }

    [Fact]
    public void AppStatusSnapshot_Current_ReturnsCurrentState()
    {
        // Arrange
        var snapshot = new AppStatusSnapshot();
        snapshot.UpdateCurrentVersion("1.0.0", out _);
        snapshot.UpdateLatestVersion("2.0.0", out _);

        // Act
        var current = snapshot.Current;

        // Assert
        Assert.Equal("1.0.0", current.CurrentVersion);
        Assert.Equal("2.0.0", current.LatestVersion);
    }

    [Fact]
    public void AppStatusSnapshot_UpdateWithNull_HandlesCorrectly()
    {
        // Arrange
        var snapshot = new AppStatusSnapshot();
        snapshot.UpdateLatestVersion("1.0.0", out _);

        // Act
        var result = snapshot.UpdateLatestVersion(null, out var status);

        // Assert
        Assert.True(result);
        Assert.Null(status.LatestVersion);
    }

    [Fact]
    public void AppStatusSnapshot_UpdateWithSameNull_ReturnsFalse()
    {
        // Arrange
        var snapshot = new AppStatusSnapshot();

        // Act - Both are null initially
        var result = snapshot.UpdateLatestVersion(null, out _);

        // Assert
        Assert.False(result);
    }

    #endregion
}
