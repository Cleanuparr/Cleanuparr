using Cleanuparr.Infrastructure.Features.LazyLibrarian;
using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events;
using Cleanuparr.Infrastructure.Events.Interfaces;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadCleaner.Services;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.DownloadRemover;
using Cleanuparr.Infrastructure.Features.DownloadRemover.Models;
using Cleanuparr.Infrastructure.Features.Files;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Features.Jobs;
using Cleanuparr.Infrastructure.Features.MalwareBlocker;
using Cleanuparr.Infrastructure.Features.Notifications;
using Cleanuparr.Infrastructure.Hubs;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Tests.Features.Jobs.TestHelpers;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.State;
using Cleanuparr.Persistence.Providers;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Cleanuparr.Infrastructure.Tests.Features.Jobs.Integration;

/// <summary>
/// Shared fixture for integration tests that wires up real services (EventPublisher, QueueItemRemover)
/// with NSubstitute mocks at external boundaries (Arr clients, download clients, notifications).
/// </summary>
public class IntegrationTestFixture : IDisposable
{
    // Real services
    public DataContext DataContext { get; private set; }
    public EventsContext EventsContext { get; private set; }
    public MemoryCache Cache { get; private set; }
    public EventPublisher EventPublisher { get; private set; } = null!;
    public QueueItemRemover QueueItemRemover { get; private set; } = null!;
    public Striker Striker { get; private set; } = null!;
    public FakeTimeProvider TimeProvider { get; private set; }

    // Mocks
    public IBus MessageBus { get; private set; }
    public IArrClientFactory ArrClientFactory { get; private set; }
    public IArrClient ArrClient { get; private set; }
    public IArrQueueIterator ArrQueueIterator { get; private set; }
    public IDownloadServiceFactory DownloadServiceFactory { get; private set; }
    public IBlocklistProvider BlocklistProvider { get; private set; }
    public IHardLinkFileService HardLinkFileService { get; private set; }
    public INotificationPublisher NotificationPublisher { get; private set; }
    public IDryRunInterceptor DryRunInterceptor { get; private set; }
    public ILazyLibrarianService LazyLibrarianService { get; private set; }
    public ILazyLibrarianServiceQC LazyLibrarianServiceQC { get; private set; }
    public ILazyLibrarianServiceCB LazyLibrarianServiceCB { get; private set; }
    public IEventPublisher EventPublisherInterface { get; private set; } = null!;
    public IHubContext<AppHub> HubContext { get; private set; }
    public ISeedingRulesCleanupService SeedingRulesService { get; private set; } = null!;
    public IUnlinkedDownloadsService UnlinkedService { get; private set; } = null!;
    public IDeadTorrentService DeadTorrentService { get; private set; } = null!;
    public IOrphanedFilesCleanupService OrphanedFilesService { get; private set; } = null!;

    // State
    public Guid JobRunId { get; private set; }
    public List<object> CapturedMessages { get; } = [];

    public IntegrationTestFixture()
    {
        SubstituteHelper.ClearPendingArgSpecs();
        DataContext = TestDataContextFactory.Create();
        EventsContext = TestEventsContextFactory.Create();
        Cache = new MemoryCache(new MemoryCacheOptions());
        TimeProvider = new FakeTimeProvider();

        MessageBus = Substitute.For<IBus>();
        ArrClientFactory = Substitute.For<IArrClientFactory>();
        ArrClient = Substitute.For<IArrClient>();
        ArrQueueIterator = Substitute.For<IArrQueueIterator>();
        DownloadServiceFactory = Substitute.For<IDownloadServiceFactory>();
        BlocklistProvider = Substitute.For<IBlocklistProvider>();
        HardLinkFileService = Substitute.For<IHardLinkFileService>();
        NotificationPublisher = Substitute.For<INotificationPublisher>();
        DryRunInterceptor = Substitute.For<IDryRunInterceptor>();
        LazyLibrarianService = Substitute.For<ILazyLibrarianService>();
        LazyLibrarianServiceQC = Substitute.For<ILazyLibrarianServiceQC>();
        LazyLibrarianServiceCB = Substitute.For<ILazyLibrarianServiceCB>();
        HubContext = CreateMockHubContext();

        SetupDefaults();
        BuildRealServices();
    }

    private void SetupDefaults()
    {
        // ArrClientFactory returns the shared ArrClient mock by default
        ArrClientFactory.GetClient(default, default).ReturnsForAnyArgs(ArrClient);

        // DryRunInterceptor returns false (not dry run) by default
        DryRunInterceptor.IsDryRunEnabled().Returns(false);
        DryRunInterceptor.InterceptAsync(Arg.Any<Func<Task>>(), Arg.Any<string?>()).ReturnsForAnyArgs(Task.CompletedTask);

        MessageBus.Publish(default(QueueItemRemoveRequest)!, default)
            .ReturnsForAnyArgs(Task.CompletedTask)
            .AndDoes(ci => CapturedMessages.Add(ci[0]));

        // Seed a JobRun so EventPublisher FK constraints are satisfied
        JobRunId = Guid.NewGuid();
        EventsContext.JobRuns.Add(new JobRun { Id = JobRunId, Type = JobType.QueueCleaner });
        EventsContext.SaveChanges();
        ContextProvider.SetJobRunId(JobRunId);
    }

    private void BuildRealServices()
    {
        EventPublisher = new EventPublisher(
            EventsContext,
            HubContext,
            Substitute.For<ILogger<EventPublisher>>(),
            NotificationPublisher,
            DryRunInterceptor,
            new SqliteDatabaseProvider());

        // Expose EventPublisher as both concrete and interface
        EventPublisherInterface = EventPublisher;

        Striker = new Striker(
            Substitute.For<ILogger<Striker>>(),
            EventsContext,
            EventPublisher,
            DryRunInterceptor);

        QueueItemRemover = new QueueItemRemover(
            Substitute.For<ILogger<QueueItemRemover>>(),
            Cache,
            ArrClientFactory,
            EventPublisher,
            EventsContext,
            DataContext,
            LazyLibrarianService);

        SeedingRulesService = new SeedingRulesCleanupService(
            Substitute.For<ILogger<SeedingRulesCleanupService>>(),
            DataContext);
        UnlinkedService = new UnlinkedDownloadsService(
            Substitute.For<ILogger<UnlinkedDownloadsService>>(),
            DataContext,
            HardLinkFileService);
        DeadTorrentService = new DeadTorrentService(
            Substitute.For<ILogger<DeadTorrentService>>(),
            DataContext,
            Striker);
        OrphanedFilesService = new OrphanedFilesCleanupService(
            Substitute.For<ILogger<OrphanedFilesCleanupService>>(),
            DataContext,
            TimeProvider,
            DryRunInterceptor);
    }

    public List<QueueItemRemoveRequest> GetCapturedRemoveRequests()
    {
        return CapturedMessages
            .OfType<QueueItemRemoveRequest>()
            .DistinctBy(r => r.Target.DownloadId)
            .ToList();
    }

    /// <summary>
    /// Runs the captured messages through the real QueueItemRemover, as the consumer would.
    /// </summary>
    public async Task ProcessCapturedRemoveRequestsAsync()
    {
        foreach (QueueItemRemoveRequest request in GetCapturedRemoveRequests())
        {
            await QueueItemRemover.RemoveQueueItemAsync(request);
        }
    }

    /// <summary>
    /// Configures the IArrQueueIterator to invoke the callback with the given records
    /// when Iterate is called for any instance.
    /// </summary>
    public void SetupArrQueueIterator(params QueueRecord[] records)
    {
        ArrQueueIterator.Iterate(
                Arg.Any<IArrClient>(),
                Arg.Any<ArrInstance>(),
                Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>())
            .Returns(ci =>
            {
                var callback = ci.Arg<Func<IReadOnlyList<QueueRecord>, Task>>();
                return callback(records);
            });
    }

    /// <summary>
    /// Creates a NSubstitute IDownloadService mock with default configuration.
    /// </summary>
    public IDownloadService CreateMockDownloadService(
        string clientName = "Test qBittorrent",
        DownloadClientTypeName typeName = DownloadClientTypeName.qBittorrent,
        DownloadClientType type = DownloadClientType.Torrent)
    {
        var mock = Substitute.For<IDownloadService>();
        mock.ClientConfig.Returns(new DownloadClientConfig
        {
            Id = Guid.NewGuid(),
            Name = clientName,
            TypeName = typeName,
            Type = type,
            Enabled = true,
            Host = new Uri("http://localhost:8080"),
            Username = "admin",
            Password = "admin"
        });
        mock.LoginAsync().Returns(Task.CompletedTask);
        return mock;
    }

    /// <summary>
    /// Registers mock download services with the factory, matched by their ClientConfig.
    /// </summary>
    public void SetupDownloadServices(params IDownloadService[] services)
    {
        foreach (var service in services)
        {
            DownloadServiceFactory.GetDownloadService(service.ClientConfig).Returns(service);
        }
    }

    /// <summary>
    /// Recreates DataContext, EventsContext, cache, and resets all mocks for a clean test.
    /// </summary>
    public void Reset()
    {
        SubstituteHelper.ClearPendingArgSpecs();
        DataContext?.Dispose();
        EventsContext?.Dispose();
        Cache?.Dispose();

        DataContext = TestDataContextFactory.Create();
        EventsContext = TestEventsContextFactory.Create();
        Cache = new MemoryCache(new MemoryCacheOptions());
        TimeProvider = new FakeTimeProvider();
        CapturedMessages.Clear();

        // Recreate all NSubstitute mocks to clear received call state
        MessageBus = Substitute.For<IBus>();
        ArrClientFactory = Substitute.For<IArrClientFactory>();
        ArrClient = Substitute.For<IArrClient>();
        ArrQueueIterator = Substitute.For<IArrQueueIterator>();
        DownloadServiceFactory = Substitute.For<IDownloadServiceFactory>();
        BlocklistProvider = Substitute.For<IBlocklistProvider>();
        HardLinkFileService = Substitute.For<IHardLinkFileService>();
        NotificationPublisher = Substitute.For<INotificationPublisher>();
        DryRunInterceptor = Substitute.For<IDryRunInterceptor>();
        HubContext = CreateMockHubContext();

        // Re-setup defaults and rebuild real services
        SetupDefaults();
        BuildRealServices();

        // Clear static state
        Striker.RecurringHashes.Clear();
    }

    private static IHubContext<AppHub> CreateMockHubContext()
    {
        var hubContext = Substitute.For<IHubContext<AppHub>>();
        var clients = Substitute.For<IHubClients>();
        var clientProxy = Substitute.For<IClientProxy>();
        clients.All.Returns(clientProxy);
        hubContext.Clients.Returns(clients);
        return hubContext;
    }

    public void Dispose()
    {
        DataContext?.Dispose();
        EventsContext?.Dispose();
        Cache?.Dispose();
        Striker.RecurringHashes.Clear();
        GC.SuppressFinalize(this);
    }
}
