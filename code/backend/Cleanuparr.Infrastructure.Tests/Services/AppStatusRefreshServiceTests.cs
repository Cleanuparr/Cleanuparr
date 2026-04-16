using System.Net;
using System.Text;
using System.Text.Json;
using Cleanuparr.Domain.Entities.AppStatus;
using Cleanuparr.Infrastructure.Hubs;
using Cleanuparr.Infrastructure.Services;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Services;

public class AppStatusRefreshServiceTests : IDisposable
{
    private readonly ILogger<AppStatusRefreshService> _logger;
    private readonly IHubContext<AppHub> _hubContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppStatusSnapshot _snapshot;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly FakeHttpMessageHandler _httpHandler;
    private readonly IServiceScopeFactory _scopeFactory;
    private AppStatusRefreshService? _service;

    public AppStatusRefreshServiceTests()
    {
        _logger = Substitute.For<ILogger<AppStatusRefreshService>>();
        _hubContext = Substitute.For<IHubContext<AppHub>>();
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _snapshot = new AppStatusSnapshot();
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        _httpHandler = new FakeHttpMessageHandler();
        _scopeFactory = Substitute.For<IServiceScopeFactory>();

        // Setup hub context
        var clients = Substitute.For<IHubClients>();
        var clientProxy = Substitute.For<IClientProxy>();
        clients.All.Returns(clientProxy);
        _hubContext.Clients.Returns(clients);
    }

    public void Dispose()
    {
        _service?.Dispose();
    }

    private AppStatusRefreshService CreateService()
    {
        _service = new AppStatusRefreshService(
            _logger,
            _hubContext,
            _httpClientFactory,
            _snapshot,
            _jsonOptions,
            _scopeFactory);
        return _service;
    }

    private void SetupHttpResponse(HttpStatusCode statusCode, string content)
    {
        _httpHandler.SetupResponse((_, _) => Task.FromResult(new HttpResponseMessage
        {
            StatusCode = statusCode,
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        }));

        var httpClient = new HttpClient(_httpHandler);
        _httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_SetsAllDependencies()
    {
        // Act
        var service = CreateService();

        // Assert
        service.ShouldNotBeNull();
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
        result.ShouldBeTrue();
        status.LatestVersion.ShouldBe("1.0.0");
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
        result.ShouldBeFalse();
        status.LatestVersion.ShouldBe("1.0.0");
    }

    [Fact]
    public void AppStatusSnapshot_UpdateCurrentVersion_ChangesStatusReturnsTrue()
    {
        // Arrange
        var snapshot = new AppStatusSnapshot();

        // Act
        var result = snapshot.UpdateCurrentVersion("2.0.0", out var status);

        // Assert
        result.ShouldBeTrue();
        status.CurrentVersion.ShouldBe("2.0.0");
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
        current.CurrentVersion.ShouldBe("1.0.0");
        current.LatestVersion.ShouldBe("2.0.0");
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
        result.ShouldBeTrue();
        status.LatestVersion.ShouldBeNull();
    }

    [Fact]
    public void AppStatusSnapshot_UpdateWithSameNull_ReturnsFalse()
    {
        // Arrange
        var snapshot = new AppStatusSnapshot();

        // Act - Both are null initially
        var result = snapshot.UpdateLatestVersion(null, out _);

        // Assert
        result.ShouldBeFalse();
    }

    #endregion
}
