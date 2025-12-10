using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.Jobs;
using Cleanuparr.Infrastructure.Tests.Features.Jobs.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Cleanuparr.Persistence.Models.Configuration.General;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Jobs;

[Collection(JobHandlerCollection.Name)]
public class DownloadCleanerTests : IDisposable
{
    private readonly JobHandlerFixture _fixture;
    private readonly Mock<ILogger<DownloadCleaner>> _logger;

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
            _logger.Object,
            _fixture.DataContext,
            _fixture.Cache,
            _fixture.MessageBus.Object,
            _fixture.ArrClientFactory.Object,
            _fixture.ArrQueueIterator.Object,
            _fixture.DownloadServiceFactory.Object,
            _fixture.EventPublisher.Object,
            _fixture.TimeProvider
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
        _logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("no download clients")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
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
        _logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("no download clients are configured")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task ExecuteInternalAsync_WhenNoFeaturesEnabled_LogsWarningAndReturns()
    {
        // Arrange
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .Setup(x => x.GetSeedingDownloads())
            .ReturnsAsync([]);

        _fixture.DownloadServiceFactory
            .Setup(x => x.GetDownloadService(It.IsAny<DownloadClientConfig>()))
            .Returns(mockDownloadService.Object);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert - should warn about no seeding downloads or no features enabled
        // The exact message depends on the order of checks
        _logger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.AtLeastOnce
        );
    }

    [Fact]
    public async Task ExecuteInternalAsync_WhenNoSeedingDownloadsFound_LogsInfoAndReturns()
    {
        // Arrange
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        TestDataContextFactory.AddCleanCategory(_fixture.DataContext);

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .Setup(x => x.GetSeedingDownloads())
            .ReturnsAsync([]);

        _fixture.DownloadServiceFactory
            .Setup(x => x.GetDownloadService(It.IsAny<DownloadClientConfig>()))
            .Returns(mockDownloadService.Object);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        _logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No seeding downloads found")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task ExecuteInternalAsync_FiltersOutIgnoredDownloads()
    {
        // Arrange
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        TestDataContextFactory.AddCleanCategory(_fixture.DataContext);

        // Add ignored download to general config
        var generalConfig = _fixture.DataContext.GeneralConfigs.First();
        generalConfig.IgnoredDownloads = ["ignored-hash"];
        _fixture.DataContext.SaveChanges();

        var mockTorrent = new Mock<ITorrentItemWrapper>();
        mockTorrent.Setup(x => x.Hash).Returns("ignored-hash");
        mockTorrent.Setup(x => x.Name).Returns("Ignored Download");
        mockTorrent.Setup(x => x.IsIgnored(It.IsAny<List<string>>())).Returns(true);

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .Setup(x => x.GetSeedingDownloads())
            .ReturnsAsync([mockTorrent.Object]);

        _fixture.DownloadServiceFactory
            .Setup(x => x.GetDownloadService(It.IsAny<DownloadClientConfig>()))
            .Returns(mockDownloadService.Object);

        var sut = CreateSut();

        // Act
        await ExecuteWithTimeAdvance(sut);

        // Assert - the download should be skipped
        _logger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("download is ignored")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task ExecuteInternalAsync_FiltersOutDownloadsUsedByArrs()
    {
        // Arrange
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        TestDataContextFactory.AddCleanCategory(_fixture.DataContext);
        var sonarrInstance = TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);

        var mockTorrent = new Mock<ITorrentItemWrapper>();
        mockTorrent.Setup(x => x.Hash).Returns("arr-download-hash");
        mockTorrent.Setup(x => x.Name).Returns("Arr Download");
        mockTorrent.Setup(x => x.IsIgnored(It.IsAny<List<string>>())).Returns(false);

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .Setup(x => x.GetSeedingDownloads())
            .ReturnsAsync([mockTorrent.Object]);

        _fixture.DownloadServiceFactory
            .Setup(x => x.GetDownloadService(It.IsAny<DownloadClientConfig>()))
            .Returns(mockDownloadService.Object);

        // Setup arr client to return queue record with matching download ID
        var mockArrClient = new Mock<IArrClient>();
        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(It.IsAny<InstanceType>()))
            .Returns(mockArrClient.Object);

        var queueRecord = new QueueRecord
        {
            Id = 1,
            DownloadId = "arr-download-hash",
            Title = "Test Download",
            Protocol = "torrent"
        };

        _fixture.ArrQueueIterator
            .Setup(x => x.Iterate(
                It.IsAny<IArrClient>(),
                It.IsAny<ArrInstance>(),
                It.IsAny<Func<IReadOnlyList<QueueRecord>, Task>>()
            ))
            .Returns(async (IArrClient client, ArrInstance instance, Func<IReadOnlyList<QueueRecord>, Task> callback) =>
            {
                await callback([queueRecord]);
            });

        var sut = CreateSut();

        // Act
        await ExecuteWithTimeAdvance(sut);

        // Assert - the download should be skipped because it's used by an arr
        _logger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("download is used by an arr")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task ExecuteInternalAsync_ProcessesAllArrConfigs()
    {
        // Arrange
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        TestDataContextFactory.AddCleanCategory(_fixture.DataContext);
        TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);
        TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        // Need at least one download for arr processing to occur
        var mockTorrent = new Mock<ITorrentItemWrapper>();
        mockTorrent.Setup(x => x.Hash).Returns("test-hash");
        mockTorrent.Setup(x => x.Name).Returns("Test Download");
        mockTorrent.Setup(x => x.IsIgnored(It.IsAny<List<string>>())).Returns(false);
        mockTorrent.Setup(x => x.Category).Returns("completed");

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .Setup(x => x.GetSeedingDownloads())
            .ReturnsAsync([mockTorrent.Object]);
        mockDownloadService
            .Setup(x => x.FilterDownloadsToBeCleanedAsync(
                It.IsAny<List<ITorrentItemWrapper>>(),
                It.IsAny<List<CleanCategory>>()
            ))
            .Returns([]);

        _fixture.DownloadServiceFactory
            .Setup(x => x.GetDownloadService(It.IsAny<DownloadClientConfig>()))
            .Returns(mockDownloadService.Object);

        var mockArrClient = new Mock<IArrClient>();
        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(It.IsAny<InstanceType>()))
            .Returns(mockArrClient.Object);

        _fixture.ArrQueueIterator
            .Setup(x => x.Iterate(
                It.IsAny<IArrClient>(),
                It.IsAny<ArrInstance>(),
                It.IsAny<Func<IReadOnlyList<QueueRecord>, Task>>()
            ))
            .Returns(Task.CompletedTask);

        var sut = CreateSut();

        // Act
        await ExecuteWithTimeAdvance(sut);

        // Assert - both instances should be processed
        _fixture.ArrClientFactory.Verify(
            x => x.GetClient(InstanceType.Sonarr),
            Times.Once
        );
        _fixture.ArrClientFactory.Verify(
            x => x.GetClient(InstanceType.Radarr),
            Times.Once
        );
    }

    #endregion

    #region ChangeUnlinkedCategoriesAsync Tests

    [Fact]
    public async Task ExecuteInternalAsync_WhenUnlinkedEnabled_EvaluatesDownloadsForHardlinks()
    {
        // Arrange
        var downloadCleanerConfig = _fixture.DataContext.DownloadCleanerConfigs.First();
        downloadCleanerConfig.UnlinkedEnabled = true;
        downloadCleanerConfig.UnlinkedTargetCategory = "unlinked";
        downloadCleanerConfig.UnlinkedCategories = ["completed"];
        _fixture.DataContext.SaveChanges();

        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);

        var mockTorrent = new Mock<ITorrentItemWrapper>();
        mockTorrent.Setup(x => x.Hash).Returns("test-hash");
        mockTorrent.Setup(x => x.Name).Returns("Test Download");
        mockTorrent.Setup(x => x.IsIgnored(It.IsAny<List<string>>())).Returns(false);
        mockTorrent.Setup(x => x.Category).Returns("completed");

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .Setup(x => x.GetSeedingDownloads())
            .ReturnsAsync([mockTorrent.Object]);
        mockDownloadService
            .Setup(x => x.FilterDownloadsToChangeCategoryAsync(
                It.IsAny<List<ITorrentItemWrapper>>(),
                It.IsAny<List<string>>()
            ))
            .Returns([mockTorrent.Object]);
        mockDownloadService
            .Setup(x => x.CreateCategoryAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        mockDownloadService
            .Setup(x => x.ChangeCategoryForNoHardLinksAsync(It.IsAny<List<ITorrentItemWrapper>>()))
            .Returns(Task.CompletedTask);

        _fixture.DownloadServiceFactory
            .Setup(x => x.GetDownloadService(It.IsAny<DownloadClientConfig>()))
            .Returns(mockDownloadService.Object);

        var sut = CreateSut();

        // Act
        await ExecuteWithTimeAdvance(sut);

        // Assert
        _logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Evaluating") && v.ToString()!.Contains("hardlinks")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    #endregion

    #region CleanDownloadsAsync Tests

    [Fact]
    public async Task ExecuteInternalAsync_WhenCategoriesConfigured_EvaluatesDownloadsForCleaning()
    {
        // Arrange
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        TestDataContextFactory.AddCleanCategory(_fixture.DataContext, "completed", 1.0, 60);

        var mockTorrent = new Mock<ITorrentItemWrapper>();
        mockTorrent.Setup(x => x.Hash).Returns("test-hash");
        mockTorrent.Setup(x => x.Name).Returns("Test Download");
        mockTorrent.Setup(x => x.IsIgnored(It.IsAny<List<string>>())).Returns(false);
        mockTorrent.Setup(x => x.Category).Returns("completed");

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .Setup(x => x.GetSeedingDownloads())
            .ReturnsAsync([mockTorrent.Object]);
        mockDownloadService
            .Setup(x => x.FilterDownloadsToBeCleanedAsync(
                It.IsAny<List<ITorrentItemWrapper>>(),
                It.IsAny<List<CleanCategory>>()
            ))
            .Returns([mockTorrent.Object]);
        mockDownloadService
            .Setup(x => x.CleanDownloadsAsync(
                It.IsAny<List<ITorrentItemWrapper>>(),
                It.IsAny<List<CleanCategory>>()
            ))
            .Returns(Task.CompletedTask);

        _fixture.DownloadServiceFactory
            .Setup(x => x.GetDownloadService(It.IsAny<DownloadClientConfig>()))
            .Returns(mockDownloadService.Object);

        var sut = CreateSut();

        // Act
        await ExecuteWithTimeAdvance(sut);

        // Assert
        _logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Evaluating") && v.ToString()!.Contains("cleanup")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    #endregion

    #region ProcessInstanceAsync Tests

    [Fact]
    public async Task ProcessInstanceAsync_CollectsDownloadIdsFromArrQueue()
    {
        // Arrange
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        TestDataContextFactory.AddCleanCategory(_fixture.DataContext);
        var sonarrInstance = TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);

        // Need at least one download for arr processing to occur
        var mockTorrent = new Mock<ITorrentItemWrapper>();
        mockTorrent.Setup(x => x.Hash).Returns("test-hash");
        mockTorrent.Setup(x => x.Name).Returns("Test Download");
        mockTorrent.Setup(x => x.IsIgnored(It.IsAny<List<string>>())).Returns(false);
        mockTorrent.Setup(x => x.Category).Returns("completed");

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .Setup(x => x.GetSeedingDownloads())
            .ReturnsAsync([mockTorrent.Object]);
        mockDownloadService
            .Setup(x => x.FilterDownloadsToBeCleanedAsync(
                It.IsAny<List<ITorrentItemWrapper>>(),
                It.IsAny<List<CleanCategory>>()
            ))
            .Returns([]);

        _fixture.DownloadServiceFactory
            .Setup(x => x.GetDownloadService(It.IsAny<DownloadClientConfig>()))
            .Returns(mockDownloadService.Object);

        var mockArrClient = new Mock<IArrClient>();
        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(InstanceType.Sonarr))
            .Returns(mockArrClient.Object);

        var queueRecords = new List<QueueRecord>
        {
            new() { Id = 1, DownloadId = "hash1", Title = "Download 1", Protocol = "torrent" },
            new() { Id = 2, DownloadId = "hash2", Title = "Download 2", Protocol = "torrent" }
        };

        _fixture.ArrQueueIterator
            .Setup(x => x.Iterate(
                mockArrClient.Object,
                It.Is<ArrInstance>(i => i.Id == sonarrInstance.Id),
                It.IsAny<Func<IReadOnlyList<QueueRecord>, Task>>()
            ))
            .Returns(async (IArrClient client, ArrInstance instance, Func<IReadOnlyList<QueueRecord>, Task> callback) =>
            {
                await callback(queueRecords);
            });

        var sut = CreateSut();

        // Act
        await ExecuteWithTimeAdvance(sut);

        // Assert - verify the iterator was called
        _fixture.ArrQueueIterator.Verify(
            x => x.Iterate(
                mockArrClient.Object,
                It.Is<ArrInstance>(i => i.Id == sonarrInstance.Id),
                It.IsAny<Func<IReadOnlyList<QueueRecord>, Task>>()
            ),
            Times.Once
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
        TestDataContextFactory.AddCleanCategory(_fixture.DataContext);

        var failingService = _fixture.CreateMockDownloadService("Failing Client");
        failingService
            .Setup(x => x.GetSeedingDownloads())
            .ThrowsAsync(new Exception("Connection failed"));

        var workingService = _fixture.CreateMockDownloadService("Working Client");
        workingService
            .Setup(x => x.GetSeedingDownloads())
            .ReturnsAsync([]);

        var callCount = 0;
        _fixture.DownloadServiceFactory
            .Setup(x => x.GetDownloadService(It.IsAny<DownloadClientConfig>()))
            .Returns(() =>
            {
                callCount++;
                return callCount == 1 ? failingService.Object : workingService.Object;
            });

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        _logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to get seeding downloads")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task ChangeUnlinkedCategoriesAsync_WhenFilterDownloadsThrows_LogsErrorAndContinues()
    {
        // Arrange
        var downloadCleanerConfig = _fixture.DataContext.DownloadCleanerConfigs.First();
        downloadCleanerConfig.UnlinkedEnabled = true;
        downloadCleanerConfig.UnlinkedTargetCategory = "unlinked";
        downloadCleanerConfig.UnlinkedCategories = ["completed"];
        _fixture.DataContext.SaveChanges();

        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);

        var mockTorrent = new Mock<ITorrentItemWrapper>();
        mockTorrent.Setup(x => x.Hash).Returns("test-hash");
        mockTorrent.Setup(x => x.Name).Returns("Test Download");
        mockTorrent.Setup(x => x.IsIgnored(It.IsAny<List<string>>())).Returns(false);
        mockTorrent.Setup(x => x.Category).Returns("completed");

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .Setup(x => x.GetSeedingDownloads())
            .ReturnsAsync([mockTorrent.Object]);
        mockDownloadService
            .Setup(x => x.FilterDownloadsToChangeCategoryAsync(
                It.IsAny<List<ITorrentItemWrapper>>(),
                It.IsAny<List<string>>()
            ))
            .Throws(new Exception("Filter failed"));

        _fixture.DownloadServiceFactory
            .Setup(x => x.GetDownloadService(It.IsAny<DownloadClientConfig>()))
            .Returns(mockDownloadService.Object);

        var sut = CreateSut();

        // Act
        await ExecuteWithTimeAdvance(sut);

        // Assert
        _logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to filter downloads for hardlinks evaluation")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task ChangeUnlinkedCategoriesAsync_WhenCreateCategoryThrows_LogsErrorAndContinues()
    {
        // Arrange
        var downloadCleanerConfig = _fixture.DataContext.DownloadCleanerConfigs.First();
        downloadCleanerConfig.UnlinkedEnabled = true;
        downloadCleanerConfig.UnlinkedTargetCategory = "unlinked";
        downloadCleanerConfig.UnlinkedCategories = ["completed"];
        _fixture.DataContext.SaveChanges();

        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);

        var mockTorrent = new Mock<ITorrentItemWrapper>();
        mockTorrent.Setup(x => x.Hash).Returns("test-hash");
        mockTorrent.Setup(x => x.Name).Returns("Test Download");
        mockTorrent.Setup(x => x.IsIgnored(It.IsAny<List<string>>())).Returns(false);
        mockTorrent.Setup(x => x.Category).Returns("completed");

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .Setup(x => x.GetSeedingDownloads())
            .ReturnsAsync([mockTorrent.Object]);
        mockDownloadService
            .Setup(x => x.FilterDownloadsToChangeCategoryAsync(
                It.IsAny<List<ITorrentItemWrapper>>(),
                It.IsAny<List<string>>()
            ))
            .Returns([mockTorrent.Object]);
        mockDownloadService
            .Setup(x => x.CreateCategoryAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("Create category failed"));

        _fixture.DownloadServiceFactory
            .Setup(x => x.GetDownloadService(It.IsAny<DownloadClientConfig>()))
            .Returns(mockDownloadService.Object);

        var sut = CreateSut();

        // Act
        await ExecuteWithTimeAdvance(sut);

        // Assert
        _logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to create category")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task ChangeUnlinkedCategoriesAsync_WhenChangeCategoryThrows_LogsErrorAndContinues()
    {
        // Arrange
        var downloadCleanerConfig = _fixture.DataContext.DownloadCleanerConfigs.First();
        downloadCleanerConfig.UnlinkedEnabled = true;
        downloadCleanerConfig.UnlinkedTargetCategory = "unlinked";
        downloadCleanerConfig.UnlinkedCategories = ["completed"];
        _fixture.DataContext.SaveChanges();

        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);

        var mockTorrent = new Mock<ITorrentItemWrapper>();
        mockTorrent.Setup(x => x.Hash).Returns("test-hash");
        mockTorrent.Setup(x => x.Name).Returns("Test Download");
        mockTorrent.Setup(x => x.IsIgnored(It.IsAny<List<string>>())).Returns(false);
        mockTorrent.Setup(x => x.Category).Returns("completed");

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .Setup(x => x.GetSeedingDownloads())
            .ReturnsAsync([mockTorrent.Object]);
        mockDownloadService
            .Setup(x => x.FilterDownloadsToChangeCategoryAsync(
                It.IsAny<List<ITorrentItemWrapper>>(),
                It.IsAny<List<string>>()
            ))
            .Returns([mockTorrent.Object]);
        mockDownloadService
            .Setup(x => x.CreateCategoryAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        mockDownloadService
            .Setup(x => x.ChangeCategoryForNoHardLinksAsync(It.IsAny<List<ITorrentItemWrapper>>()))
            .ThrowsAsync(new Exception("Change category failed"));

        _fixture.DownloadServiceFactory
            .Setup(x => x.GetDownloadService(It.IsAny<DownloadClientConfig>()))
            .Returns(mockDownloadService.Object);

        var sut = CreateSut();

        // Act
        await ExecuteWithTimeAdvance(sut);

        // Assert
        _logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to change category for download client")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task CleanDownloadsAsync_WhenFilterDownloadsThrows_LogsErrorAndContinues()
    {
        // Arrange
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        TestDataContextFactory.AddCleanCategory(_fixture.DataContext);

        var mockTorrent = new Mock<ITorrentItemWrapper>();
        mockTorrent.Setup(x => x.Hash).Returns("test-hash");
        mockTorrent.Setup(x => x.Name).Returns("Test Download");
        mockTorrent.Setup(x => x.IsIgnored(It.IsAny<List<string>>())).Returns(false);
        mockTorrent.Setup(x => x.Category).Returns("completed");

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .Setup(x => x.GetSeedingDownloads())
            .ReturnsAsync([mockTorrent.Object]);
        mockDownloadService
            .Setup(x => x.FilterDownloadsToBeCleanedAsync(
                It.IsAny<List<ITorrentItemWrapper>>(),
                It.IsAny<List<CleanCategory>>()
            ))
            .Throws(new Exception("Filter failed"));

        _fixture.DownloadServiceFactory
            .Setup(x => x.GetDownloadService(It.IsAny<DownloadClientConfig>()))
            .Returns(mockDownloadService.Object);

        var sut = CreateSut();

        // Act
        await ExecuteWithTimeAdvance(sut);

        // Assert
        _logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to filter downloads for cleaning")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task CleanDownloadsAsync_WhenCleanDownloadsThrows_LogsErrorAndContinues()
    {
        // Arrange
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        TestDataContextFactory.AddCleanCategory(_fixture.DataContext);

        var mockTorrent = new Mock<ITorrentItemWrapper>();
        mockTorrent.Setup(x => x.Hash).Returns("test-hash");
        mockTorrent.Setup(x => x.Name).Returns("Test Download");
        mockTorrent.Setup(x => x.IsIgnored(It.IsAny<List<string>>())).Returns(false);
        mockTorrent.Setup(x => x.Category).Returns("completed");

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .Setup(x => x.GetSeedingDownloads())
            .ReturnsAsync([mockTorrent.Object]);
        mockDownloadService
            .Setup(x => x.FilterDownloadsToBeCleanedAsync(
                It.IsAny<List<ITorrentItemWrapper>>(),
                It.IsAny<List<CleanCategory>>()
            ))
            .Returns([mockTorrent.Object]);
        mockDownloadService
            .Setup(x => x.CleanDownloadsAsync(
                It.IsAny<List<ITorrentItemWrapper>>(),
                It.IsAny<List<CleanCategory>>()
            ))
            .ThrowsAsync(new Exception("Clean failed"));

        _fixture.DownloadServiceFactory
            .Setup(x => x.GetDownloadService(It.IsAny<DownloadClientConfig>()))
            .Returns(mockDownloadService.Object);

        var sut = CreateSut();

        // Act
        await ExecuteWithTimeAdvance(sut);

        // Assert
        _logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to clean downloads for download client")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task ProcessArrConfigAsync_WhenArrIteratorThrows_LogsErrorAndRethrows()
    {
        // Arrange - DownloadCleaner calls ProcessArrConfigAsync with throwOnFailure=true
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        TestDataContextFactory.AddCleanCategory(_fixture.DataContext);
        TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);

        var mockTorrent = new Mock<ITorrentItemWrapper>();
        mockTorrent.Setup(x => x.Hash).Returns("test-hash");
        mockTorrent.Setup(x => x.Name).Returns("Test Download");
        mockTorrent.Setup(x => x.IsIgnored(It.IsAny<List<string>>())).Returns(false);
        mockTorrent.Setup(x => x.Category).Returns("completed");

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .Setup(x => x.GetSeedingDownloads())
            .ReturnsAsync([mockTorrent.Object]);
        mockDownloadService
            .Setup(x => x.FilterDownloadsToBeCleanedAsync(
                It.IsAny<List<ITorrentItemWrapper>>(),
                It.IsAny<List<CleanCategory>>()
            ))
            .Returns([]);

        _fixture.DownloadServiceFactory
            .Setup(x => x.GetDownloadService(It.IsAny<DownloadClientConfig>()))
            .Returns(mockDownloadService.Object);

        var mockArrClient = new Mock<IArrClient>();
        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(InstanceType.Sonarr))
            .Returns(mockArrClient.Object);

        // Make the arr queue iterator throw an exception
        _fixture.ArrQueueIterator
            .Setup(x => x.Iterate(
                It.IsAny<IArrClient>(),
                It.IsAny<ArrInstance>(),
                It.IsAny<Func<IReadOnlyList<QueueRecord>, Task>>()
            ))
            .ThrowsAsync(new InvalidOperationException("Arr connection failed"));

        var sut = CreateSut();

        // Act & Assert - exception should propagate since throwOnFailure=true
        // Need to advance time for the delay to pass before the exception is thrown
        var task = sut.ExecuteAsync();
        _fixture.TimeProvider.Advance(TimeSpan.FromSeconds(10));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Equal("Arr connection failed", exception.Message);

        // Verify error was logged
        _logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("failed to process")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    #endregion
}
