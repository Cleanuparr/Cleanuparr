using Cleanuparr.Infrastructure.Events.Interfaces;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.Files;
using Cleanuparr.Infrastructure.Features.Jobs;
using Cleanuparr.Infrastructure.Features.MalwareBlocker;
using Cleanuparr.Persistence;
using MassTransit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Cleanuparr.Infrastructure.Tests.Features.Jobs.TestHelpers;

/// <summary>
/// Base fixture for job handler tests providing common mock dependencies
/// </summary>
public class JobHandlerFixture : IDisposable
{
    public DataContext DataContext { get; private set; }
    public MemoryCache Cache { get; }
    public Mock<IBus> MessageBus { get; }
    public Mock<IArrClientFactory> ArrClientFactory { get; }
    public Mock<IArrQueueIterator> ArrQueueIterator { get; }
    public Mock<IDownloadServiceFactory> DownloadServiceFactory { get; }
    public Mock<IEventPublisher> EventPublisher { get; }
    public Mock<IBlocklistProvider> BlocklistProvider { get; }
    public Mock<IHardLinkFileService> HardLinkFileService { get; }
    public FakeTimeProvider TimeProvider { get; private set; }

    public JobHandlerFixture()
    {
        DataContext = TestDataContextFactory.Create();
        Cache = new MemoryCache(new MemoryCacheOptions());
        MessageBus = new Mock<IBus>();
        ArrClientFactory = new Mock<IArrClientFactory>();
        ArrQueueIterator = new Mock<IArrQueueIterator>();
        DownloadServiceFactory = new Mock<IDownloadServiceFactory>();
        EventPublisher = new Mock<IEventPublisher>();
        BlocklistProvider = new Mock<IBlocklistProvider>();
        HardLinkFileService = new Mock<IHardLinkFileService>();
        TimeProvider = new FakeTimeProvider();

        // Setup default behaviors
        SetupDefaultBehaviors();

        // Setup JobRunId in context for tests
        ContextProvider.SetJobRunId(Guid.NewGuid());
    }

    private void SetupDefaultBehaviors()
    {
        // EventPublisher methods return completed task by default
        EventPublisher
            .Setup(x => x.PublishAsync(
                It.IsAny<Domain.Enums.EventType>(),
                It.IsAny<string>(),
                It.IsAny<Domain.Enums.EventSeverity>(),
                It.IsAny<object?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>()))
            .Returns(Task.CompletedTask);
    }

    /// <summary>
    /// Creates a mock logger for a specific handler type
    /// </summary>
    public Mock<ILogger<T>> CreateLogger<T>() where T : GenericHandler
    {
        return new Mock<ILogger<T>>();
    }

    /// <summary>
    /// Creates a mock download service
    /// </summary>
    public Mock<IDownloadService> CreateMockDownloadService(string clientName = "Test Client")
    {
        var mock = new Mock<IDownloadService>();
        mock.Setup(x => x.ClientConfig).Returns(new Persistence.Models.Configuration.DownloadClientConfig
        {
            Id = Guid.NewGuid(),
            Name = clientName,
            Type = Domain.Enums.DownloadClientType.Torrent,
            TypeName = Domain.Enums.DownloadClientTypeName.qBittorrent,
            Enabled = true,
            Host = new Uri("http://localhost:8080")
        });
        mock.Setup(x => x.LoginAsync()).Returns(Task.CompletedTask);
        return mock;
    }

    /// <summary>
    /// Sets up the DownloadServiceFactory to return the specified mock services
    /// </summary>
    public void SetupDownloadServices(params Mock<IDownloadService>[] services)
    {
        foreach (var service in services)
        {
            DownloadServiceFactory
                .Setup(x => x.GetDownloadService(service.Object.ClientConfig))
                .Returns(service.Object);
        }
    }

    /// <summary>
    /// Creates a fresh DataContext, disposing the old one
    /// </summary>
    public DataContext RecreateDataContext(bool seedData = true)
    {
        DataContext?.Dispose();
        DataContext = TestDataContextFactory.Create(seedData);
        return DataContext;
    }

    public void ResetMocks()
    {
        MessageBus.Reset();
        ArrClientFactory.Reset();
        ArrQueueIterator.Reset();
        DownloadServiceFactory.Reset();
        EventPublisher.Reset();
        BlocklistProvider.Reset();
        HardLinkFileService.Reset();
        Cache.Clear();
        TimeProvider = new FakeTimeProvider();

        SetupDefaultBehaviors();

        // Setup fresh JobRunId for each test
        ContextProvider.SetJobRunId(Guid.NewGuid());
    }

    public void Dispose()
    {
        DataContext?.Dispose();
        Cache?.Dispose();
        GC.SuppressFinalize(this);
    }
}
