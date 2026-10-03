using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events;
using Cleanuparr.Infrastructure.Features.DryRun;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.General;
using Cleanuparr.Persistence.Models.Events;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Events;

public class EventCleanupServiceTests : IDisposable
{
    private readonly ILogger<EventCleanupService> _logger;
    private readonly ServiceCollection _services;
    private readonly IServiceProvider _serviceProvider;
    private readonly string _dbName;

    public EventCleanupServiceTests()
    {
        _logger = Substitute.For<ILogger<EventCleanupService>>();
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
        var service = new EventCleanupService(_logger, scopeFactory, TimeProvider.System);
        var cts = new CancellationTokenSource();

        // Act - start and immediately cancel
        cts.CancelAfter(100);
        await service.StartAsync(cts.Token);
        await Task.Delay(200); // Give it time to process
        await service.StopAsync(CancellationToken.None);

        // Assert
        _logger.HasLogContaining(LogLevel.Information, "started").ShouldBeTrue();
    }

    [Fact]
    public async Task StopAsync_LogsStopMessage()
    {
        // Arrange
        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var service = new EventCleanupService(_logger, scopeFactory, TimeProvider.System);
        var cts = new CancellationTokenSource();

        // Act
        cts.CancelAfter(50);
        await service.StartAsync(cts.Token);
        await Task.Delay(100);
        await service.StopAsync(CancellationToken.None);

        // Assert
        _logger.HasLogContaining(LogLevel.Information, "stopping").ShouldBeTrue();
    }

    [Fact]
    public void Constructor_InitializesWithCorrectParameters()
    {
        // Arrange
        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();

        // Act
        var service = new EventCleanupService(_logger, scopeFactory, TimeProvider.System);

        // Assert - service should be created without exception
        service.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_GracefullyHandlesCancellation()
    {
        // Arrange
        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var service = new EventCleanupService(_logger, scopeFactory, TimeProvider.System);
        var cts = new CancellationTokenSource();

        // Act - cancel immediately
        cts.Cancel();

        // Start should not throw
        await service.StartAsync(cts.Token);
        await Task.Delay(50);
        await service.StopAsync(CancellationToken.None);

        // Assert - should have logged stopped message
        _logger.HasLogContaining(LogLevel.Information, "stopped").ShouldBeTrue();
    }

    [Fact]
    public async Task PerformCleanupAsync_DryRunOff_PurgesDryRunData()
    {
        // Arrange
        IDryRunPurger dryRunPurger = Substitute.For<IDryRunPurger>();
        IServiceProvider provider = BuildProviderWithGeneralConfig(dryRunOn: false, dryRunPurger);
        var service = new EventCleanupService(_logger, provider.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System);

        // Act
        await service.PerformCleanupAsync();

        // Assert
        await dryRunPurger.Received(1).PurgeAsync();
    }

    [Fact]
    public async Task PerformCleanupAsync_DryRunOn_DoesNotPurge()
    {
        // Arrange
        IDryRunPurger dryRunPurger = Substitute.For<IDryRunPurger>();
        IServiceProvider provider = BuildProviderWithGeneralConfig(dryRunOn: true, dryRunPurger);
        var service = new EventCleanupService(_logger, provider.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System);

        // Act
        await service.PerformCleanupAsync();

        // Assert
        await dryRunPurger.DidNotReceive().PurgeAsync();
    }

    private IServiceProvider BuildProviderWithGeneralConfig(bool dryRunOn, IDryRunPurger dryRunPurger)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        _services.AddDbContext<DataContext>(options => options.UseSqlite(connection));
        _services.AddSingleton(dryRunPurger);
        IServiceProvider provider = _services.BuildServiceProvider();

        using IServiceScope scope = provider.CreateScope();
        DataContext dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();
        dataContext.Database.EnsureCreated();
        dataContext.GeneralConfigs.Add(new GeneralConfig
        {
            Id = Guid.NewGuid(),
            DryRun = dryRunOn,
            IgnoredDownloads = [],
            Log = new LoggingConfig(),
        });
        dataContext.SaveChanges();

        return provider;
    }
}
