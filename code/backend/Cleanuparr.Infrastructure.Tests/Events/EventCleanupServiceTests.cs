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
using Microsoft.Extensions.Time.Testing;
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
        EventCleanupService service = new EventCleanupService(_logger, scopeFactory, TimeProvider.System, new DryRunActivity());
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
        EventCleanupService service = new EventCleanupService(_logger, scopeFactory, TimeProvider.System, new DryRunActivity());
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
        EventCleanupService service = new EventCleanupService(_logger, scopeFactory, TimeProvider.System, new DryRunActivity());

        // Assert - service should be created without exception
        service.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_GracefullyHandlesCancellation()
    {
        // Arrange
        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        EventCleanupService service = new EventCleanupService(_logger, scopeFactory, TimeProvider.System, new DryRunActivity());
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
        EventCleanupService service = new EventCleanupService(_logger, provider.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System, new DryRunActivity());

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
        EventCleanupService service = new EventCleanupService(_logger, provider.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System, new DryRunActivity());

        // Act
        await service.PerformCleanupAsync();

        // Assert
        await dryRunPurger.DidNotReceive().PurgeAsync();
    }

    [Fact]
    public async Task PerformCleanupAsync_PurgeThrows_ReturnsTrue()
    {
        // Arrange
        IDryRunPurger dryRunPurger = Substitute.For<IDryRunPurger>();
        dryRunPurger.PurgeAsync().Returns(Task.FromException(new InvalidOperationException("boom")));
        IServiceProvider provider = BuildProviderWithGeneralConfig(dryRunOn: false, dryRunPurger);
        EventCleanupService service = new EventCleanupService(_logger, provider.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System, new DryRunActivity());

        // Act
        bool purgeFailed = await service.PerformCleanupAsync();

        // Assert
        purgeFailed.ShouldBeTrue();
    }

    [Fact]
    public async Task PerformCleanupAsync_PurgeSucceeds_ReturnsFalse()
    {
        // Arrange
        IDryRunPurger dryRunPurger = Substitute.For<IDryRunPurger>();
        IServiceProvider provider = BuildProviderWithGeneralConfig(dryRunOn: false, dryRunPurger);
        EventCleanupService service = new EventCleanupService(_logger, provider.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System, new DryRunActivity());

        // Act
        bool purgeFailed = await service.PerformCleanupAsync();

        // Assert
        purgeFailed.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0, 30)]
    [InlineData(1, 30)]
    [InlineData(2, 60)]
    [InlineData(3, 300)]
    [InlineData(10, 300)]
    public void GetRetryDelay_ReturnsExpectedDelay(int failures, int expectedSeconds)
    {
        // Act
        TimeSpan delay = EventCleanupService.GetRetryDelay(failures);

        // Assert
        delay.ShouldBe(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Fact]
    public async Task WaitForNextRunAsync_DelayElapses_Completes()
    {
        // Arrange
        IDryRunPurger dryRunPurger = Substitute.For<IDryRunPurger>();
        IServiceProvider provider = BuildProviderWithGeneralConfig(dryRunOn: false, dryRunPurger);
        FakeTimeProvider timeProvider = new();
        EventCleanupService service = new EventCleanupService(_logger, provider.GetRequiredService<IServiceScopeFactory>(), timeProvider, new DryRunActivity());

        // Act
        Task waitTask = service.WaitForNextRunAsync(TimeSpan.FromMinutes(5), CancellationToken.None);
        await Task.Yield();
        bool completedBeforeAdvance = waitTask.IsCompleted;

        timeProvider.Advance(TimeSpan.FromMinutes(5));
        await waitTask;

        // Assert
        completedBeforeAdvance.ShouldBeFalse();
        waitTask.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public async Task WaitForNextRunAsync_SignalWithDryRunOff_CompletesImmediately()
    {
        // Arrange
        IDryRunPurger dryRunPurger = Substitute.For<IDryRunPurger>();
        IServiceProvider provider = BuildProviderWithGeneralConfig(dryRunOn: false, dryRunPurger);
        FakeTimeProvider timeProvider = new();
        DryRunActivity dryRunActivity = new();
        EventCleanupService service = new EventCleanupService(_logger, provider.GetRequiredService<IServiceScopeFactory>(), timeProvider, dryRunActivity);

        // Act
        Task waitTask = service.WaitForNextRunAsync(TimeSpan.FromMinutes(5), CancellationToken.None);
        dryRunActivity.RequestPurge();
        await waitTask;

        // Assert
        waitTask.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public async Task WaitForNextRunAsync_SignalWithDryRunOn_StaysPendingUntilDelayElapses()
    {
        // Arrange
        IDryRunPurger dryRunPurger = Substitute.For<IDryRunPurger>();
        IServiceProvider provider = BuildProviderWithGeneralConfig(dryRunOn: true, dryRunPurger);
        FakeTimeProvider timeProvider = new();
        DryRunActivity dryRunActivity = new();
        EventCleanupService service = new EventCleanupService(_logger, provider.GetRequiredService<IServiceScopeFactory>(), timeProvider, dryRunActivity);

        // Act
        Task waitTask = service.WaitForNextRunAsync(TimeSpan.FromMinutes(5), CancellationToken.None);
        dryRunActivity.RequestPurge();

        // give the signal branch a chance to run and re-read the config
        for (int i = 0; i < 10 && !waitTask.IsCompleted; i++)
        {
            await Task.Yield();
        }
        bool completedAfterSignal = waitTask.IsCompleted;

        timeProvider.Advance(TimeSpan.FromMinutes(5));
        await waitTask;

        // Assert
        completedAfterSignal.ShouldBeFalse();
        waitTask.IsCompleted.ShouldBeTrue();
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
