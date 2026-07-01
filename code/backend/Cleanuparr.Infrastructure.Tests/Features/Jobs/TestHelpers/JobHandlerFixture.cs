using Cleanuparr.Infrastructure.Events.Interfaces;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadCleaner.Services;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.Files;
using Cleanuparr.Infrastructure.Features.Jobs;
using Cleanuparr.Infrastructure.Features.MalwareBlocker;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Services.Interfaces;
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
    public IJobManagementService JobManagementService { get; private set; }
    public IHardLinkFileService HardLinkFileService { get; private set; }
    public IDryRunInterceptor DryRunInterceptor { get; private set; }
    public FakeTimeProvider TimeProvider { get; private set; }
    public ISeedingRulesCleanupService SeedingRulesService { get; private set; }
    public IUnlinkedDownloadsService UnlinkedService { get; private set; }
    public IDeadTorrentService DeadTorrentService { get; private set; }
    public IOrphanedFilesCleanupService OrphanedFilesService { get; private set; }
    public ILogger<SeedingRulesCleanupService> SeedingRulesLogger { get; private set; }
    public ILogger<UnlinkedDownloadsService> UnlinkedLogger { get; private set; }
    public ILogger<OrphanedFilesCleanupService> OrphanedFilesLogger { get; private set; }

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
        JobManagementService = Substitute.For<IJobManagementService>();
        HardLinkFileService = Substitute.For<IHardLinkFileService>();
        DryRunInterceptor = Substitute.For<IDryRunInterceptor>();
        TimeProvider = new FakeTimeProvider();
        RecreateCleanupServices();

        // Setup default behaviors
        SetupDefaultBehaviors();

        // Setup JobRunId in context for tests
        ContextProvider.SetJobRunId(Guid.NewGuid());
    }

    /// <summary>
    /// Builds real cleanup services bound to the current DataContext/mocks.
    /// Tests can replace any of them with substitutes before constructing
    /// the SUT.
    /// </summary>
    private void RecreateCleanupServices()
    {
        SeedingRulesLogger = Substitute.For<ILogger<SeedingRulesCleanupService>>();
        UnlinkedLogger = Substitute.For<ILogger<UnlinkedDownloadsService>>();
        OrphanedFilesLogger = Substitute.For<ILogger<OrphanedFilesCleanupService>>();
        SeedingRulesService = new SeedingRulesCleanupService(SeedingRulesLogger, DataContext);
        UnlinkedService = new UnlinkedDownloadsService(UnlinkedLogger, DataContext, HardLinkFileService);
        DeadTorrentService = Substitute.For<IDeadTorrentService>();
        OrphanedFilesService = new OrphanedFilesCleanupService(
            OrphanedFilesLogger,
            DataContext,
            TimeProvider,
            DryRunInterceptor);
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
        RecreateCleanupServices();
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
        JobManagementService = Substitute.For<IJobManagementService>();
        HardLinkFileService = Substitute.For<IHardLinkFileService>();
        DryRunInterceptor = Substitute.For<IDryRunInterceptor>();
        Cache.Clear();
        TimeProvider = new FakeTimeProvider();
        RecreateCleanupServices();

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
