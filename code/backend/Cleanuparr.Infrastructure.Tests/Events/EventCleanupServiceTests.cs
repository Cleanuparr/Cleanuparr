using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Events;

public class EventCleanupServiceTests : IDisposable
{
    private readonly Mock<ILogger<EventCleanupService>> _loggerMock;
    private readonly ServiceCollection _services;
    private readonly IServiceProvider _serviceProvider;
    private readonly string _dbName;

    public EventCleanupServiceTests()
    {
        _loggerMock = new Mock<ILogger<EventCleanupService>>();
        _services = new ServiceCollection();
        _dbName = Guid.NewGuid().ToString();

        // Setup in-memory database for testing
        _services.AddDbContext<EventsContext>(options =>
            options.UseInMemoryDatabase(databaseName: _dbName));

        _serviceProvider = _services.BuildServiceProvider();
    }

    public void Dispose()
    {
        // Cleanup the in-memory database
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventsContext>();
        context.Database.EnsureDeleted();
    }

    [Fact]
    public async Task ExecuteAsync_LogsStartMessage()
    {
        // Arrange
        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var service = new EventCleanupService(_loggerMock.Object, scopeFactory);
        var cts = new CancellationTokenSource();

        // Act - start and immediately cancel
        cts.CancelAfter(100);
        await service.StartAsync(cts.Token);
        await Task.Delay(200); // Give it time to process
        await service.StopAsync(CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("started")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StopAsync_LogsStopMessage()
    {
        // Arrange
        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var service = new EventCleanupService(_loggerMock.Object, scopeFactory);
        var cts = new CancellationTokenSource();

        // Act
        cts.CancelAfter(50);
        await service.StartAsync(cts.Token);
        await Task.Delay(100);
        await service.StopAsync(CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("stopping")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_InitializesWithCorrectParameters()
    {
        // Arrange
        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();

        // Act
        var service = new EventCleanupService(_loggerMock.Object, scopeFactory);

        // Assert - service should be created without exception
        Assert.NotNull(service);
    }

    [Fact]
    public async Task ExecuteAsync_GracefullyHandlesCancellation()
    {
        // Arrange
        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var service = new EventCleanupService(_loggerMock.Object, scopeFactory);
        var cts = new CancellationTokenSource();

        // Act - cancel immediately
        cts.Cancel();

        // Start should not throw
        await service.StartAsync(cts.Token);
        await Task.Delay(50);
        await service.StopAsync(CancellationToken.None);

        // Assert - should have logged stopped message
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("stopped")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
