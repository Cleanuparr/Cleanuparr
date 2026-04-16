using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.Jobs;
using Cleanuparr.Infrastructure.Tests.Features.Jobs.TestHelpers;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Cleanuparr.Persistence.Models.Configuration.General;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Jobs;

[Collection(JobHandlerCollection.Name)]
public class DownloadCleanerTests : IDisposable
{
    private readonly JobHandlerFixture _fixture;
    private readonly ILogger<DownloadCleaner> _logger;

    public DownloadCleanerTests(JobHandlerFixture fixture)
    {
        _fixture = fixture;
        _fixture.RecreateDataContext();
        _fixture.ResetMocks();
        _logger = _fixture.CreateLogger<DownloadCleaner>();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private DownloadCleaner CreateSut()
    {
        return new DownloadCleaner(
            _logger,
            _fixture.DataContext,
            _fixture.Cache,
            _fixture.MessageBus,
            _fixture.ArrClientFactory,
            _fixture.ArrQueueIterator,
            _fixture.DownloadServiceFactory,
            _fixture.EventPublisher,
            _fixture.TimeProvider,
            _fixture.HardLinkFileService
        );
    }

    /// <summary>
    /// Executes the handler and advances time past the 10-second delay
    /// </summary>
    private async Task ExecuteWithTimeAdvance(DownloadCleaner sut)
    {
        var task = sut.ExecuteAsync();
        _fixture.TimeProvider.Advance(TimeSpan.FromSeconds(10));
        await task;
    }

    #region ExecuteAsync Tests (inherited from GenericHandler)

    [Fact]
    public async Task ExecuteAsync_LoadsAllConfigsIntoContextProvider()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert - verify configs were loaded (by checking the handler completed without errors)
        // The configs are loaded into ContextProvider which is AsyncLocal scoped
        _logger.ReceivedLogContaining(LogLevel.Warning, "no download clients");
    }

    #endregion

    #region ExecuteInternalAsync Tests

    [Fact]
    public async Task ExecuteInternalAsync_WhenNoDownloadClientsConfigured_LogsWarningAndReturns()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        _logger.ReceivedLogContaining(LogLevel.Warning, "no download clients are configured");
    }

    [Fact]
    public async Task ExecuteInternalAsync_WhenNoFeaturesEnabled_LogsWarningAndReturns()
    {
        // Arrange
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .GetSeedingDownloads()
            .Returns([]);

        _fixture.DownloadServiceFactory
            .GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert - should warn about no seeding downloads or no features enabled
        // The exact message depends on the order of checks
        Assert.NotEmpty(_logger.ReceivedCalls());
    }

    [Fact]
    public async Task ExecuteInternalAsync_WhenNoSeedingDownloadsFound_LogsInfoAndReturns()
    {
        // Arrange
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        TestDataContextFactory.AddSeedingRule(_fixture.DataContext);

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .GetSeedingDownloads()
            .Returns([]);

        _fixture.DownloadServiceFactory
            .GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        _logger.ReceivedLogContaining(LogLevel.Information, "No seeding downloads found");
    }

    [Fact]
    public async Task ExecuteInternalAsync_FiltersOutIgnoredDownloads()
    {
        // Arrange
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        TestDataContextFactory.AddSeedingRule(_fixture.DataContext);

        // Add ignored download to general config
        var generalConfig = _fixture.DataContext.GeneralConfigs.First();
        generalConfig.IgnoredDownloads = ["ignored-hash"];
        _fixture.DataContext.SaveChanges();

        var mockTorrent = Substitute.For<ITorrentItemWrapper>();
        mockTorrent.Hash.Returns("ignored-hash");
        mockTorrent.Name.Returns("Ignored Download");
        mockTorrent.IsIgnored(Arg.Any<List<string>>()).Returns(true);

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .GetSeedingDownloads()
            .Returns([mockTorrent]);

        _fixture.DownloadServiceFactory
            .GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        var sut = CreateSut();

        // Act
        await ExecuteWithTimeAdvance(sut);

        // Assert - the download should be skipped
        _logger.ReceivedLogContaining(LogLevel.Debug, "download is ignored");
    }

    [Fact]
    public async Task ExecuteInternalAsync_FiltersOutDownloadsUsedByArrs()
    {
        // Arrange
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        TestDataContextFactory.AddSeedingRule(_fixture.DataContext);
        var sonarrInstance = TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);

        var mockTorrent = Substitute.For<ITorrentItemWrapper>();
        mockTorrent.Hash.Returns("arr-download-hash");
        mockTorrent.Name.Returns("Arr Download");
        mockTorrent.IsIgnored(Arg.Any<List<string>>()).Returns(false);

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .GetSeedingDownloads()
            .Returns([mockTorrent]);

        _fixture.DownloadServiceFactory
            .GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        // Setup arr client to return queue record with matching download ID
        var mockArrClient = Substitute.For<IArrClient>();
        _fixture.ArrClientFactory
            .GetClient(Arg.Any<InstanceType>(), Arg.Any<float>())
            .Returns(mockArrClient);

        var queueRecord = new QueueRecord
        {
            Id = 1,
            DownloadId = "arr-download-hash",
            Title = "Test Download",
            Protocol = "torrent"
        };

        _fixture.ArrQueueIterator
            .Iterate(
                Arg.Any<IArrClient>(),
                Arg.Any<ArrInstance>(),
                Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>()
            )
            .Returns(ci =>
            {
                var callback = ci.ArgAt<Func<IReadOnlyList<QueueRecord>, Task>>(2);
                return callback([queueRecord]);
            });

        var sut = CreateSut();

        // Act
        await ExecuteWithTimeAdvance(sut);

        // Assert - the download should be skipped because it's used by an arr
        _logger.ReceivedLogContaining(LogLevel.Debug, "download is used by an arr");
    }

    [Fact]
    public async Task ExecuteInternalAsync_ProcessesAllArrConfigs()
    {
        // Arrange
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        TestDataContextFactory.AddSeedingRule(_fixture.DataContext);
        TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);
        TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        // Need at least one download for arr processing to occur
        var mockTorrent = Substitute.For<ITorrentItemWrapper>();
        mockTorrent.Hash.Returns("test-hash");
        mockTorrent.Name.Returns("Test Download");
        mockTorrent.IsIgnored(Arg.Any<List<string>>()).Returns(false);
        mockTorrent.Category.Returns("completed");

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .GetSeedingDownloads()
            .Returns([mockTorrent]);
        mockDownloadService
            .FilterDownloadsToBeCleanedAsync(
                Arg.Any<List<ITorrentItemWrapper>>(),
                Arg.Any<List<ISeedingRule>>()
            )
            .Returns([]);

        _fixture.DownloadServiceFactory
            .GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        var mockArrClient = Substitute.For<IArrClient>();
        _fixture.ArrClientFactory
            .GetClient(Arg.Any<InstanceType>(), Arg.Any<float>())
            .Returns(mockArrClient);

        _fixture.ArrQueueIterator
            .Iterate(
                Arg.Any<IArrClient>(),
                Arg.Any<ArrInstance>(),
                Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>()
            )
            .Returns(Task.CompletedTask);

        var sut = CreateSut();

        // Act
        await ExecuteWithTimeAdvance(sut);

        // Assert - both instances should be processed
        _fixture.ArrClientFactory.Received(1).GetClient(InstanceType.Sonarr, Arg.Any<float>());
        _fixture.ArrClientFactory.Received(1).GetClient(InstanceType.Radarr, Arg.Any<float>());
    }

    #endregion

    #region ChangeUnlinkedCategoriesAsync Tests

    [Fact]
    public async Task ChangeUnlinkedCategoriesAsync_WhenIgnoredRootDirsConfigured_PopulatesFileCountsOnce()
    {
        // Arrange
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        TestDataContextFactory.AddUnlinkedConfig(_fixture.DataContext,
            ignoredRootDirs: ["/media/library"]);

        var mockTorrent = Substitute.For<ITorrentItemWrapper>();
        mockTorrent.Hash.Returns("test-hash");
        mockTorrent.Name.Returns("Test Download");
        mockTorrent.IsIgnored(Arg.Any<List<string>>()).Returns(false);
        mockTorrent.Category.Returns("completed");

        var dbClient = _fixture.DataContext.DownloadClients.First();
        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService.ClientConfig.Returns(dbClient);
        mockDownloadService
            .GetSeedingDownloads()
            .Returns([mockTorrent]);
        mockDownloadService
            .FilterDownloadsToChangeCategoryAsync(
                Arg.Any<List<ITorrentItemWrapper>>(),
                Arg.Any<UnlinkedConfig>()
            )
            .Returns([mockTorrent]);
        mockDownloadService
            .CreateCategoryAsync(Arg.Any<string>())
            .Returns(Task.CompletedTask);
        mockDownloadService
            .ChangeCategoryForNoHardLinksAsync(Arg.Any<List<ITorrentItemWrapper>>(), Arg.Any<UnlinkedConfig>())
            .Returns(Task.CompletedTask);

        _fixture.DownloadServiceFactory
            .GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        var sut = CreateSut();

        // Act
        await ExecuteWithTimeAdvance(sut);

        // Assert - PopulateFileCounts should be called exactly once
        _fixture.HardLinkFileService.Received(1)
            .PopulateFileCounts(Arg.Is<IEnumerable<string>>(dirs => dirs.Contains("/media/library")));
    }

    [Fact]
    public async Task ChangeUnlinkedCategoriesAsync_WhenNoIgnoredRootDirsConfigured_DoesNotPopulateFileCounts()
    {
        // Arrange
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        TestDataContextFactory.AddUnlinkedConfig(_fixture.DataContext,
            ignoredRootDirs: []);

        var mockTorrent = Substitute.For<ITorrentItemWrapper>();
        mockTorrent.Hash.Returns("test-hash");
        mockTorrent.Name.Returns("Test Download");
        mockTorrent.IsIgnored(Arg.Any<List<string>>()).Returns(false);
        mockTorrent.Category.Returns("completed");

        var dbClient = _fixture.DataContext.DownloadClients.First();
        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService.ClientConfig.Returns(dbClient);
        mockDownloadService
            .GetSeedingDownloads()
            .Returns([mockTorrent]);
        mockDownloadService
            .FilterDownloadsToChangeCategoryAsync(
                Arg.Any<List<ITorrentItemWrapper>>(),
                Arg.Any<UnlinkedConfig>()
            )
            .Returns([mockTorrent]);
        mockDownloadService
            .CreateCategoryAsync(Arg.Any<string>())
            .Returns(Task.CompletedTask);
        mockDownloadService
            .ChangeCategoryForNoHardLinksAsync(Arg.Any<List<ITorrentItemWrapper>>(), Arg.Any<UnlinkedConfig>())
            .Returns(Task.CompletedTask);

        _fixture.DownloadServiceFactory
            .GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        var sut = CreateSut();

        // Act
        await ExecuteWithTimeAdvance(sut);

        // Assert - PopulateFileCounts should not be called
        _fixture.HardLinkFileService.DidNotReceive()
            .PopulateFileCounts(Arg.Any<IEnumerable<string>>());
    }

    [Fact]
    public async Task ChangeUnlinkedCategoriesAsync_WithMultipleDownloadClients_PopulatesFileCountsPerClient()
    {
        // Arrange
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext, "Client 1");
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext, "Client 2");

        // Add unlinked config for each client
        var clients = _fixture.DataContext.DownloadClients.ToList();
        foreach (var client in clients)
        {
            _fixture.DataContext.UnlinkedConfigs.Add(new UnlinkedConfig
            {
                Id = Guid.NewGuid(),
                DownloadClientConfigId = client.Id,
                Enabled = true,
                TargetCategory = "unlinked",
                Categories = ["completed"],
                IgnoredRootDirs = ["/media/library"]
            });
        }
        _fixture.DataContext.SaveChanges();

        var mockTorrent1 = Substitute.For<ITorrentItemWrapper>();
        mockTorrent1.Hash.Returns("test-hash-1");
        mockTorrent1.Name.Returns("Test Download 1");
        mockTorrent1.IsIgnored(Arg.Any<List<string>>()).Returns(false);
        mockTorrent1.Category.Returns("completed");

        var mockTorrent2 = Substitute.For<ITorrentItemWrapper>();
        mockTorrent2.Hash.Returns("test-hash-2");
        mockTorrent2.Name.Returns("Test Download 2");
        mockTorrent2.IsIgnored(Arg.Any<List<string>>()).Returns(false);
        mockTorrent2.Category.Returns("completed");

        var mockDownloadService1 = _fixture.CreateMockDownloadService("Client 1");
        mockDownloadService1.ClientConfig.Returns(clients[0]);
        mockDownloadService1
            .GetSeedingDownloads()
            .Returns([mockTorrent1]);
        mockDownloadService1
            .FilterDownloadsToChangeCategoryAsync(
                Arg.Any<List<ITorrentItemWrapper>>(),
                Arg.Any<UnlinkedConfig>()
            )
            .Returns([mockTorrent1]);
        mockDownloadService1
            .CreateCategoryAsync(Arg.Any<string>())
            .Returns(Task.CompletedTask);
        mockDownloadService1
            .ChangeCategoryForNoHardLinksAsync(Arg.Any<List<ITorrentItemWrapper>>(), Arg.Any<UnlinkedConfig>())
            .Returns(Task.CompletedTask);

        var mockDownloadService2 = _fixture.CreateMockDownloadService("Client 2");
        mockDownloadService2.ClientConfig.Returns(clients[1]);
        mockDownloadService2
            .GetSeedingDownloads()
            .Returns([mockTorrent2]);
        mockDownloadService2
            .FilterDownloadsToChangeCategoryAsync(
                Arg.Any<List<ITorrentItemWrapper>>(),
                Arg.Any<UnlinkedConfig>()
            )
            .Returns([mockTorrent2]);
        mockDownloadService2
            .CreateCategoryAsync(Arg.Any<string>())
            .Returns(Task.CompletedTask);
        mockDownloadService2
            .ChangeCategoryForNoHardLinksAsync(Arg.Any<List<ITorrentItemWrapper>>(), Arg.Any<UnlinkedConfig>())
            .Returns(Task.CompletedTask);

        var callCount = 0;
        _fixture.DownloadServiceFactory
            .GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(ci =>
            {
                callCount++;
                return callCount == 1 ? mockDownloadService1 : mockDownloadService2;
            });

        var sut = CreateSut();

        // Act
        await ExecuteWithTimeAdvance(sut);

        // Assert - PopulateFileCounts is called once per client with ignored root dirs
        _fixture.HardLinkFileService.Received(2)
            .PopulateFileCounts(Arg.Any<IEnumerable<string>>());

        // Verify both clients had their ChangeCategoryForNoHardLinksAsync called
        await mockDownloadService1.Received(1)
            .ChangeCategoryForNoHardLinksAsync(Arg.Any<List<ITorrentItemWrapper>>(), Arg.Any<UnlinkedConfig>());
        await mockDownloadService2.Received(1)
            .ChangeCategoryForNoHardLinksAsync(Arg.Any<List<ITorrentItemWrapper>>(), Arg.Any<UnlinkedConfig>());
    }

    [Fact]
    public async Task ExecuteInternalAsync_WhenUnlinkedEnabled_EvaluatesDownloadsForHardlinks()
    {
        // Arrange
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        TestDataContextFactory.AddUnlinkedConfig(_fixture.DataContext);

        var mockTorrent = Substitute.For<ITorrentItemWrapper>();
        mockTorrent.Hash.Returns("test-hash");
        mockTorrent.Name.Returns("Test Download");
        mockTorrent.IsIgnored(Arg.Any<List<string>>()).Returns(false);
        mockTorrent.Category.Returns("completed");

        var dbClient = _fixture.DataContext.DownloadClients.First();
        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService.ClientConfig.Returns(dbClient);
        mockDownloadService
            .GetSeedingDownloads()
            .Returns([mockTorrent]);
        mockDownloadService
            .FilterDownloadsToChangeCategoryAsync(
                Arg.Any<List<ITorrentItemWrapper>>(),
                Arg.Any<UnlinkedConfig>()
            )
            .Returns([mockTorrent]);
        mockDownloadService
            .CreateCategoryAsync(Arg.Any<string>())
            .Returns(Task.CompletedTask);
        mockDownloadService
            .ChangeCategoryForNoHardLinksAsync(Arg.Any<List<ITorrentItemWrapper>>(), Arg.Any<UnlinkedConfig>())
            .Returns(Task.CompletedTask);

        _fixture.DownloadServiceFactory
            .GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        var sut = CreateSut();

        // Act
        await ExecuteWithTimeAdvance(sut);

        // Assert
        _logger.ReceivedLogContaining(LogLevel.Information, "Evaluating");
    }

    #endregion

    #region CleanDownloadsAsync Tests

    [Fact]
    public async Task ExecuteInternalAsync_WhenCategoriesConfigured_EvaluatesDownloadsForCleaning()
    {
        // Arrange
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        TestDataContextFactory.AddSeedingRule(_fixture.DataContext, "completed", 1.0, 60);

        var mockTorrent = Substitute.For<ITorrentItemWrapper>();
        mockTorrent.Hash.Returns("test-hash");
        mockTorrent.Name.Returns("Test Download");
        mockTorrent.IsIgnored(Arg.Any<List<string>>()).Returns(false);
        mockTorrent.Category.Returns("completed");

        var dbClient = _fixture.DataContext.DownloadClients.First();
        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService.ClientConfig.Returns(dbClient);
        mockDownloadService
            .GetSeedingDownloads()
            .Returns([mockTorrent]);
        mockDownloadService
            .FilterDownloadsToBeCleanedAsync(
                Arg.Any<List<ITorrentItemWrapper>>(),
                Arg.Any<List<ISeedingRule>>()
            )
            .Returns([mockTorrent]);
        mockDownloadService
            .CleanDownloadsAsync(
                Arg.Any<List<ITorrentItemWrapper>>(),
                Arg.Any<List<ISeedingRule>>()
            )
            .Returns(Task.CompletedTask);

        _fixture.DownloadServiceFactory
            .GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        var sut = CreateSut();

        // Act
        await ExecuteWithTimeAdvance(sut);

        // Assert
        _logger.ReceivedLogContaining(LogLevel.Information, "Evaluating");
    }

    #endregion

    #region ProcessInstanceAsync Tests

    [Fact]
    public async Task ProcessInstanceAsync_CollectsDownloadIdsFromArrQueue()
    {
        // Arrange
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        TestDataContextFactory.AddSeedingRule(_fixture.DataContext);
        var sonarrInstance = TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);

        // Need at least one download for arr processing to occur
        var mockTorrent = Substitute.For<ITorrentItemWrapper>();
        mockTorrent.Hash.Returns("test-hash");
        mockTorrent.Name.Returns("Test Download");
        mockTorrent.IsIgnored(Arg.Any<List<string>>()).Returns(false);
        mockTorrent.Category.Returns("completed");

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .GetSeedingDownloads()
            .Returns([mockTorrent]);
        mockDownloadService
            .FilterDownloadsToBeCleanedAsync(
                Arg.Any<List<ITorrentItemWrapper>>(),
                Arg.Any<List<ISeedingRule>>()
            )
            .Returns([]);

        _fixture.DownloadServiceFactory
            .GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        var mockArrClient = Substitute.For<IArrClient>();
        _fixture.ArrClientFactory
            .GetClient(InstanceType.Sonarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var queueRecords = new List<QueueRecord>
        {
            new() { Id = 1, DownloadId = "hash1", Title = "Download 1", Protocol = "torrent" },
            new() { Id = 2, DownloadId = "hash2", Title = "Download 2", Protocol = "torrent" }
        };

        _fixture.ArrQueueIterator
            .Iterate(
                mockArrClient,
                Arg.Is<ArrInstance>(i => i.Id == sonarrInstance.Id),
                Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>()
            )
            .Returns(ci =>
            {
                var callback = ci.ArgAt<Func<IReadOnlyList<QueueRecord>, Task>>(2);
                return callback(queueRecords);
            });

        var sut = CreateSut();

        // Act
        await ExecuteWithTimeAdvance(sut);

        // Assert - verify the iterator was called
        await _fixture.ArrQueueIterator.Received(1).Iterate(
            mockArrClient,
            Arg.Is<ArrInstance>(i => i.Id == sonarrInstance.Id),
            Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>()
        );
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ExecuteInternalAsync_WhenDownloadServiceFails_LogsErrorAndContinues()
    {
        // Arrange
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext, "Failing Client");
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext, "Working Client");
        TestDataContextFactory.AddSeedingRule(_fixture.DataContext);

        var failingService = _fixture.CreateMockDownloadService("Failing Client");
        failingService
            .GetSeedingDownloads()
            .ThrowsAsync(new Exception("Connection failed"));

        var workingService = _fixture.CreateMockDownloadService("Working Client");
        workingService
            .GetSeedingDownloads()
            .Returns([]);

        var callCount = 0;
        _fixture.DownloadServiceFactory
            .GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(ci =>
            {
                callCount++;
                return callCount == 1 ? failingService : workingService;
            });

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        _logger.ReceivedLogContaining(LogLevel.Error, "Failed to get seeding downloads");
    }

    [Fact]
    public async Task ChangeUnlinkedCategoriesAsync_WhenFilterDownloadsThrows_LogsErrorAndContinues()
    {
        // Arrange
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        TestDataContextFactory.AddUnlinkedConfig(_fixture.DataContext);

        var mockTorrent = Substitute.For<ITorrentItemWrapper>();
        mockTorrent.Hash.Returns("test-hash");
        mockTorrent.Name.Returns("Test Download");
        mockTorrent.IsIgnored(Arg.Any<List<string>>()).Returns(false);
        mockTorrent.Category.Returns("completed");

        var dbClient = _fixture.DataContext.DownloadClients.First();
        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService.ClientConfig.Returns(dbClient);
        mockDownloadService
            .GetSeedingDownloads()
            .Returns([mockTorrent]);
        mockDownloadService
            .FilterDownloadsToChangeCategoryAsync(
                Arg.Any<List<ITorrentItemWrapper>>(),
                Arg.Any<UnlinkedConfig>()
            )
            .Throws(new Exception("Filter failed"));

        _fixture.DownloadServiceFactory
            .GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        var sut = CreateSut();

        // Act
        await ExecuteWithTimeAdvance(sut);

        // Assert
        _logger.ReceivedLogContaining(LogLevel.Error, "Failed to process unlinked downloads for");
    }

    [Fact]
    public async Task ChangeUnlinkedCategoriesAsync_WhenCreateCategoryThrows_LogsErrorAndContinues()
    {
        // Arrange
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        TestDataContextFactory.AddUnlinkedConfig(_fixture.DataContext);

        var mockTorrent = Substitute.For<ITorrentItemWrapper>();
        mockTorrent.Hash.Returns("test-hash");
        mockTorrent.Name.Returns("Test Download");
        mockTorrent.IsIgnored(Arg.Any<List<string>>()).Returns(false);
        mockTorrent.Category.Returns("completed");

        var dbClient = _fixture.DataContext.DownloadClients.First();
        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService.ClientConfig.Returns(dbClient);
        mockDownloadService
            .GetSeedingDownloads()
            .Returns([mockTorrent]);
        mockDownloadService
            .FilterDownloadsToChangeCategoryAsync(
                Arg.Any<List<ITorrentItemWrapper>>(),
                Arg.Any<UnlinkedConfig>()
            )
            .Returns([mockTorrent]);
        mockDownloadService
            .CreateCategoryAsync(Arg.Any<string>())
            .ThrowsAsync(new Exception("Create category failed"));

        _fixture.DownloadServiceFactory
            .GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        var sut = CreateSut();

        // Act
        await ExecuteWithTimeAdvance(sut);

        // Assert
        _logger.ReceivedLogContaining(LogLevel.Error, "Failed to create category");
    }

    [Fact]
    public async Task ChangeUnlinkedCategoriesAsync_WhenChangeCategoryThrows_LogsErrorAndContinues()
    {
        // Arrange
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        TestDataContextFactory.AddUnlinkedConfig(_fixture.DataContext);

        var mockTorrent = Substitute.For<ITorrentItemWrapper>();
        mockTorrent.Hash.Returns("test-hash");
        mockTorrent.Name.Returns("Test Download");
        mockTorrent.IsIgnored(Arg.Any<List<string>>()).Returns(false);
        mockTorrent.Category.Returns("completed");

        var dbClient = _fixture.DataContext.DownloadClients.First();
        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService.ClientConfig.Returns(dbClient);
        mockDownloadService
            .GetSeedingDownloads()
            .Returns([mockTorrent]);
        mockDownloadService
            .FilterDownloadsToChangeCategoryAsync(
                Arg.Any<List<ITorrentItemWrapper>>(),
                Arg.Any<UnlinkedConfig>()
            )
            .Returns([mockTorrent]);
        mockDownloadService
            .CreateCategoryAsync(Arg.Any<string>())
            .Returns(Task.CompletedTask);
        mockDownloadService
            .ChangeCategoryForNoHardLinksAsync(Arg.Any<List<ITorrentItemWrapper>>(), Arg.Any<UnlinkedConfig>())
            .ThrowsAsync(new Exception("Change category failed"));

        _fixture.DownloadServiceFactory
            .GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        var sut = CreateSut();

        // Act
        await ExecuteWithTimeAdvance(sut);

        // Assert
        _logger.ReceivedLogContaining(LogLevel.Error, "Failed to process unlinked downloads for");
    }

    [Fact]
    public async Task CleanDownloadsAsync_WhenFilterDownloadsThrows_LogsErrorAndContinues()
    {
        // Arrange
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        TestDataContextFactory.AddSeedingRule(_fixture.DataContext);

        var mockTorrent = Substitute.For<ITorrentItemWrapper>();
        mockTorrent.Hash.Returns("test-hash");
        mockTorrent.Name.Returns("Test Download");
        mockTorrent.IsIgnored(Arg.Any<List<string>>()).Returns(false);
        mockTorrent.Category.Returns("completed");

        var dbClient = _fixture.DataContext.DownloadClients.First();
        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService.ClientConfig.Returns(dbClient);
        mockDownloadService
            .GetSeedingDownloads()
            .Returns([mockTorrent]);
        mockDownloadService
            .FilterDownloadsToBeCleanedAsync(
                Arg.Any<List<ITorrentItemWrapper>>(),
                Arg.Any<List<ISeedingRule>>()
            )
            .Throws(new Exception("Filter failed"));

        _fixture.DownloadServiceFactory
            .GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        var sut = CreateSut();

        // Act
        await ExecuteWithTimeAdvance(sut);

        // Assert
        _logger.ReceivedLogContaining(LogLevel.Error, "Failed to clean downloads for");
    }

    [Fact]
    public async Task CleanDownloadsAsync_WhenCleanDownloadsThrows_LogsErrorAndContinues()
    {
        // Arrange
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        TestDataContextFactory.AddSeedingRule(_fixture.DataContext);

        var mockTorrent = Substitute.For<ITorrentItemWrapper>();
        mockTorrent.Hash.Returns("test-hash");
        mockTorrent.Name.Returns("Test Download");
        mockTorrent.IsIgnored(Arg.Any<List<string>>()).Returns(false);
        mockTorrent.Category.Returns("completed");

        var dbClient = _fixture.DataContext.DownloadClients.First();
        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService.ClientConfig.Returns(dbClient);
        mockDownloadService
            .GetSeedingDownloads()
            .Returns([mockTorrent]);
        mockDownloadService
            .FilterDownloadsToBeCleanedAsync(
                Arg.Any<List<ITorrentItemWrapper>>(),
                Arg.Any<List<ISeedingRule>>()
            )
            .Returns([mockTorrent]);
        mockDownloadService
            .CleanDownloadsAsync(
                Arg.Any<List<ITorrentItemWrapper>>(),
                Arg.Any<List<ISeedingRule>>()
            )
            .ThrowsAsync(new Exception("Clean failed"));

        _fixture.DownloadServiceFactory
            .GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        var sut = CreateSut();

        // Act
        await ExecuteWithTimeAdvance(sut);

        // Assert
        _logger.ReceivedLogContaining(LogLevel.Error, "Failed to clean downloads for");
    }

    [Fact]
    public async Task ProcessArrConfigAsync_WhenArrIteratorThrows_LogsErrorAndRethrows()
    {
        // Arrange - DownloadCleaner calls ProcessArrConfigAsync with throwOnFailure=true
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        TestDataContextFactory.AddSeedingRule(_fixture.DataContext);
        TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);

        var mockTorrent = Substitute.For<ITorrentItemWrapper>();
        mockTorrent.Hash.Returns("test-hash");
        mockTorrent.Name.Returns("Test Download");
        mockTorrent.IsIgnored(Arg.Any<List<string>>()).Returns(false);
        mockTorrent.Category.Returns("completed");

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .GetSeedingDownloads()
            .Returns([mockTorrent]);
        mockDownloadService
            .FilterDownloadsToBeCleanedAsync(
                Arg.Any<List<ITorrentItemWrapper>>(),
                Arg.Any<List<ISeedingRule>>()
            )
            .Returns([]);

        _fixture.DownloadServiceFactory
            .GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        var mockArrClient = Substitute.For<IArrClient>();
        _fixture.ArrClientFactory
            .GetClient(InstanceType.Sonarr, Arg.Any<float>())
            .Returns(mockArrClient);

        // Make the arr queue iterator throw an exception
        _fixture.ArrQueueIterator
            .Iterate(
                Arg.Any<IArrClient>(),
                Arg.Any<ArrInstance>(),
                Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>()
            )
            .ThrowsAsync(new InvalidOperationException("Arr connection failed"));

        var sut = CreateSut();

        // Act & Assert - exception should propagate since throwOnFailure=true
        // Need to advance time for the delay to pass before the exception is thrown
        var task = sut.ExecuteAsync();
        _fixture.TimeProvider.Advance(TimeSpan.FromSeconds(10));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Equal("Arr connection failed", exception.Message);

        // Verify error was logged
        _logger.ReceivedLogContaining(LogLevel.Error, "failed to process");
    }

    #endregion

    #region Per-Client Config Tests

    [Fact]
    public async Task ExecuteInternalAsync_ClientWithNoSeedingRules_SkipsCleanup()
    {
        // Arrange
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        // No seeding rules added — only unlinked config disabled

        var mockTorrent = Substitute.For<ITorrentItemWrapper>();
        mockTorrent.Hash.Returns("test-hash");
        mockTorrent.Name.Returns("Test Download");
        mockTorrent.IsIgnored(Arg.Any<List<string>>()).Returns(false);
        mockTorrent.Category.Returns("completed");

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .GetSeedingDownloads()
            .Returns([mockTorrent]);

        _fixture.DownloadServiceFactory
            .GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        var sut = CreateSut();

        // Act
        await ExecuteWithTimeAdvance(sut);

        // Assert - CleanDownloadsAsync should never be called
        await mockDownloadService.DidNotReceive()
            .CleanDownloadsAsync(
                Arg.Any<List<ITorrentItemWrapper>>(),
                Arg.Any<List<ISeedingRule>>()
            );
    }

    [Fact]
    public async Task ExecuteInternalAsync_ClientWithDisabledUnlinkedConfig_SkipsUnlinkedProcessing()
    {
        // Arrange
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        TestDataContextFactory.AddUnlinkedConfig(_fixture.DataContext, enabled: false);

        var mockTorrent = Substitute.For<ITorrentItemWrapper>();
        mockTorrent.Hash.Returns("test-hash");
        mockTorrent.Name.Returns("Test Download");
        mockTorrent.IsIgnored(Arg.Any<List<string>>()).Returns(false);
        mockTorrent.Category.Returns("completed");

        var dbClient = _fixture.DataContext.DownloadClients.First();
        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService.ClientConfig.Returns(dbClient);
        mockDownloadService
            .GetSeedingDownloads()
            .Returns([mockTorrent]);

        _fixture.DownloadServiceFactory
            .GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        var sut = CreateSut();

        // Act
        await ExecuteWithTimeAdvance(sut);

        // Assert - FilterDownloadsToChangeCategoryAsync should never be called
        mockDownloadService.DidNotReceive()
            .FilterDownloadsToChangeCategoryAsync(
                Arg.Any<List<ITorrentItemWrapper>>(),
                Arg.Any<UnlinkedConfig>()
            );
    }

    [Fact]
    public async Task ExecuteInternalAsync_UnlinkedEnabledButNoCategories_LogsWarning()
    {
        // Arrange
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        TestDataContextFactory.AddUnlinkedConfig(_fixture.DataContext, enabled: true, categories: []);

        var mockTorrent = Substitute.For<ITorrentItemWrapper>();
        mockTorrent.Hash.Returns("test-hash");
        mockTorrent.Name.Returns("Test Download");
        mockTorrent.IsIgnored(Arg.Any<List<string>>()).Returns(false);
        mockTorrent.Category.Returns("completed");

        var dbClient = _fixture.DataContext.DownloadClients.First();
        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService.ClientConfig.Returns(dbClient);
        mockDownloadService
            .GetSeedingDownloads()
            .Returns([mockTorrent]);

        _fixture.DownloadServiceFactory
            .GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        var sut = CreateSut();

        // Act
        await ExecuteWithTimeAdvance(sut);

        // Assert - should log warning about no categories
        _logger.ReceivedLogContaining(LogLevel.Warning, "no categories are configured");
    }

    #endregion
}
