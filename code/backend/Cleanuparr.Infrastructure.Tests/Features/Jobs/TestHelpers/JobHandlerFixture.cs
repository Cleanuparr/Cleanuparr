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
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using NSubstitute;

namespace Cleanuparr.Infrastructure.Tests.Features.Jobs.TestHelpers;

/// <summary>
/// Base fixture for job handler tests providing common mock dependencies
/// </summary>
public class JobHandlerFixture : IDisposable
{
    public DataContext DataContext { get; private set; }
    public MemoryCache Cache { get; private set; }
    public IBus MessageBus { get; private set; }
    public IArrClientFactory ArrClientFactory { get; private set; }
    public IArrQueueIterator ArrQueueIterator { get; private set; }
    public IDownloadServiceFactory DownloadServiceFactory { get; private set; }
    public IEventPublisher EventPublisher { get; private set; }
    public IBlocklistProvider BlocklistProvider { get; private set; }
    public IHardLinkFileService HardLinkFileService { get; private set; }
    public FakeTimeProvider TimeProvider { get; private set; }

    public JobHandlerFixture()
    {
        SubstituteHelper.ClearPendingArgSpecs();
        DataContext = TestDataContextFactory.Create();
        Cache = new MemoryCache(new MemoryCacheOptions());
        MessageBus = Substitute.For<IBus>();
        ArrClientFactory = Substitute.For<IArrClientFactory>();
        ArrQueueIterator = Substitute.For<IArrQueueIterator>();
        DownloadServiceFactory = Substitute.For<IDownloadServiceFactory>();
        EventPublisher = Substitute.For<IEventPublisher>();
        BlocklistProvider = Substitute.For<IBlocklistProvider>();
        HardLinkFileService = Substitute.For<IHardLinkFileService>();
        TimeProvider = new FakeTimeProvider();

        // Setup default behaviors
        SetupDefaultBehaviors();

        // Setup JobRunId in context for tests
        ContextProvider.SetJobRunId(Guid.NewGuid());
    }

    private void SetupDefaultBehaviors()
    {
        // EventPublisher methods return completed task by default
        EventPublisher.PublishAsync(
                default, default!, default, default, default, default)
            .ReturnsForAnyArgs(Task.CompletedTask);
    }

    /// <summary>
    /// Creates a mock logger for a specific handler type
    /// </summary>
    public ILogger<T> CreateLogger<T>() where T : GenericHandler
    {
        return Substitute.For<ILogger<T>>();
    }

    /// <summary>
    /// Creates a mock download service
    /// </summary>
    public IDownloadService CreateMockDownloadService(string clientName = "Test Client")
    {
        var mock = Substitute.For<IDownloadService>();
        mock.ClientConfig.Returns(new Persistence.Models.Configuration.DownloadClientConfig
        {
            Id = Guid.NewGuid(),
            Name = clientName,
            Type = Domain.Enums.DownloadClientType.Torrent,
            TypeName = Domain.Enums.DownloadClientTypeName.qBittorrent,
            Enabled = true,
            Host = new Uri("http://localhost:8080")
        });
        mock.LoginAsync().Returns(Task.CompletedTask);
        return mock;
    }

    /// <summary>
    /// Sets up the DownloadServiceFactory to return the specified mock services
    /// </summary>
    public void SetupDownloadServices(params IDownloadService[] services)
    {
        foreach (var service in services)
        {
            DownloadServiceFactory.GetDownloadService(service.ClientConfig).Returns(service);
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
        SubstituteHelper.ClearPendingArgSpecs();
        // Recreate all substitutes to clear received call state
        MessageBus = Substitute.For<IBus>();
        ArrClientFactory = Substitute.For<IArrClientFactory>();
        ArrQueueIterator = Substitute.For<IArrQueueIterator>();
        DownloadServiceFactory = Substitute.For<IDownloadServiceFactory>();
        EventPublisher = Substitute.For<IEventPublisher>();
        BlocklistProvider = Substitute.For<IBlocklistProvider>();
        HardLinkFileService = Substitute.For<IHardLinkFileService>();
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
