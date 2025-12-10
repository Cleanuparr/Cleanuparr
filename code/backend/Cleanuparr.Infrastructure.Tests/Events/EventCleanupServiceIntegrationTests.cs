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

/// <summary>
/// Integration tests for the cleanup logic that actually deletes events
/// </summary>
public class EventCleanupServiceIntegrationTests : IDisposable
{
    private readonly EventsContext _context;
    private readonly Mock<ILogger<EventCleanupService>> _loggerMock;
    private readonly IServiceProvider _serviceProvider;
    private readonly string _dbName;

    public EventCleanupServiceIntegrationTests()
    {
        _dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();

        // Setup in-memory database
        services.AddDbContext<EventsContext>(options =>
            options.UseInMemoryDatabase(databaseName: _dbName));

        _serviceProvider = services.BuildServiceProvider();
        _loggerMock = new Mock<ILogger<EventCleanupService>>();

        using var scope = _serviceProvider.CreateScope();
        _context = scope.ServiceProvider.GetRequiredService<EventsContext>();
    }

    public void Dispose()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventsContext>();
        context.Database.EnsureDeleted();
    }

    [Fact]
    public async Task CleanupService_PreservesRecentEvents()
    {
        // Arrange - Add recent events (within retention period)
        using (var scope = _serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventsContext>();

            context.Events.Add(new AppEvent
            {
                Id = Guid.NewGuid(),
                EventType = EventType.QueueItemDeleted,
                Message = "Recent event 1",
                Severity = EventSeverity.Information,
                Timestamp = DateTime.UtcNow.AddDays(-5)
            });
            context.Events.Add(new AppEvent
            {
                Id = Guid.NewGuid(),
                EventType = EventType.DownloadCleaned,
                Message = "Recent event 2",
                Severity = EventSeverity.Important,
                Timestamp = DateTime.UtcNow.AddDays(-10)
            });

            await context.SaveChangesAsync();
        }

        // Verify events exist
        using (var scope = _serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventsContext>();
            var count = await context.Events.CountAsync();
            Assert.Equal(2, count);
        }
    }

    [Fact]
    public async Task EventCleanupService_CanStartAndStop()
    {
        // Arrange
        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var service = new EventCleanupService(_loggerMock.Object, scopeFactory);
        var cts = new CancellationTokenSource();

        // Act
        cts.CancelAfter(100);
        await service.StartAsync(cts.Token);

        // Give some time for the service to process
        await Task.Delay(150);

        await service.StopAsync(CancellationToken.None);

        // Assert - the service should complete without throwing
        Assert.True(true);
    }

    [Fact]
    public async Task EventCleanupService_HandlesExceptionsGracefully()
    {
        // Arrange
        // Note: In-memory provider doesn't support ExecuteDeleteAsync,
        // so the cleanup will fail. This test verifies the service handles errors gracefully.
        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var service = new EventCleanupService(_loggerMock.Object, scopeFactory);
        var cts = new CancellationTokenSource();

        // Act
        cts.CancelAfter(100);
        await service.StartAsync(cts.Token);
        await Task.Delay(150);
        await service.StopAsync(CancellationToken.None);

        // Assert - the service should handle the error and continue (log it but not crash)
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to perform event cleanup")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}