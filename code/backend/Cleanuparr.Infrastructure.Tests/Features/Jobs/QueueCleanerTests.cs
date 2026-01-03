using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.DownloadRemover.Models;
using Cleanuparr.Infrastructure.Helpers;
using Cleanuparr.Infrastructure.Tests.Features.Jobs.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Data.Models.Arr;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using QueueCleanerJob = Cleanuparr.Infrastructure.Features.Jobs.QueueCleaner;

namespace Cleanuparr.Infrastructure.Tests.Features.Jobs;

[Collection(JobHandlerCollection.Name)]
public class QueueCleanerTests : IDisposable
{
    private readonly JobHandlerFixture _fixture;
    private readonly Mock<ILogger<QueueCleanerJob>> _logger;

    public QueueCleanerTests(JobHandlerFixture fixture)
    {
        _fixture = fixture;
        _fixture.RecreateDataContext();
        _fixture.ResetMocks();
        _logger = _fixture.CreateLogger<QueueCleanerJob>();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private QueueCleanerJob CreateSut()
    {
        return new QueueCleanerJob(
            _logger.Object,
            _fixture.DataContext,
            _fixture.Cache,
            _fixture.MessageBus.Object,
            _fixture.ArrClientFactory.Object,
            _fixture.ArrQueueIterator.Object,
            _fixture.DownloadServiceFactory.Object,
            _fixture.EventPublisher.Object
        );
    }

    #region ExecuteInternalAsync Tests

    [Fact]
    public async Task ExecuteInternalAsync_LoadsStallRulesFromDatabase()
    {
        // Arrange
        TestDataContextFactory.AddStallRule(_fixture.DataContext, enabled: true);
        TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);

        var mockArrClient = new Mock<IArrClient>();
        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(It.IsAny<InstanceType>(), It.IsAny<float>()))
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
        await sut.ExecuteAsync();

        // Assert - no debug message about no active stall rules
        _logger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No active stall rules found")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Never
        );
    }

    [Fact]
    public async Task ExecuteInternalAsync_WhenNoStallRules_LogsDebugMessage()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        _logger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No active stall rules found")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task ExecuteInternalAsync_LoadsSlowRulesFromDatabase()
    {
        // Arrange
        TestDataContextFactory.AddSlowRule(_fixture.DataContext, enabled: true);
        TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);

        var mockArrClient = new Mock<IArrClient>();
        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(It.IsAny<InstanceType>(), It.IsAny<float>()))
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
        await sut.ExecuteAsync();

        // Assert - no debug message about no active slow rules
        _logger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No active slow rules found")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Never
        );
    }

    [Fact]
    public async Task ExecuteInternalAsync_WhenNoSlowRules_LogsDebugMessage()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        _logger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No active slow rules found")),
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
        TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);
        TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        var mockArrClient = new Mock<IArrClient>();
        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(It.IsAny<InstanceType>(), It.IsAny<float>()))
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
        await sut.ExecuteAsync();

        // Assert
        _fixture.ArrClientFactory.Verify(x => x.GetClient(InstanceType.Sonarr, It.IsAny<float>()), Times.Once);
        _fixture.ArrClientFactory.Verify(x => x.GetClient(InstanceType.Radarr, It.IsAny<float>()), Times.Once);
    }

    #endregion

    #region ProcessInstanceAsync Tests

    [Fact]
    public async Task ProcessInstanceAsync_SkipsIgnoredDownloads()
    {
        // Arrange
        var generalConfig = _fixture.DataContext.GeneralConfigs.First();
        generalConfig.IgnoredDownloads = ["ignored-download-id"];
        _fixture.DataContext.SaveChanges();

        TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);

        var mockArrClient = new Mock<IArrClient>();
        mockArrClient.Setup(x => x.IsRecordValid(It.IsAny<QueueRecord>())).Returns(true);

        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(InstanceType.Sonarr, It.IsAny<float>()))
            .Returns(mockArrClient.Object);

        var queueRecord = new QueueRecord
        {
            Id = 1,
            DownloadId = "ignored-download-id",
            Title = "Ignored Download",
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
        await sut.ExecuteAsync();

        // Assert
        _logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("download is ignored")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task ProcessInstanceAsync_SkipsAlreadyCachedDownloads()
    {
        // Arrange
        var sonarrInstance = TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);

        // Pre-cache the download using the correct cache key format
        var cacheKey = CacheKeys.DownloadMarkedForRemoval("cached-download-id", sonarrInstance.Url);
        _fixture.Cache.Set(cacheKey, true);

        var mockArrClient = new Mock<IArrClient>();
        mockArrClient.Setup(x => x.IsRecordValid(It.IsAny<QueueRecord>())).Returns(true);

        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(InstanceType.Sonarr, It.IsAny<float>()))
            .Returns(mockArrClient.Object);

        var queueRecord = new QueueRecord
        {
            Id = 1,
            DownloadId = "cached-download-id",
            Title = "Cached Download",
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
        await sut.ExecuteAsync();

        // Assert
        _logger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("already marked for removal")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task ProcessInstanceAsync_ChecksTorrentClientsForDownloadInfo()
    {
        // Arrange
        var sonarrInstance = TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);

        var mockArrClient = new Mock<IArrClient>();
        mockArrClient.Setup(x => x.IsRecordValid(It.IsAny<QueueRecord>())).Returns(true);
        mockArrClient.Setup(x => x.ShouldRemoveFromQueue(
            It.IsAny<InstanceType>(),
            It.IsAny<QueueRecord>(),
            It.IsAny<bool>(),
            It.IsAny<short>()
        )).ReturnsAsync(false);

        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(InstanceType.Sonarr, It.IsAny<float>()))
            .Returns(mockArrClient.Object);

        var queueRecord = new QueueRecord
        {
            Id = 1,
            DownloadId = "torrent-download-id",
            Title = "Torrent Download",
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

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .Setup(x => x.ShouldRemoveFromArrQueueAsync(
                It.IsAny<string>(),
                It.IsAny<List<string>>()
            ))
            .ReturnsAsync(new DownloadCheckResult { Found = true, ShouldRemove = false });

        _fixture.DownloadServiceFactory
            .Setup(x => x.GetDownloadService(It.IsAny<DownloadClientConfig>()))
            .Returns(mockDownloadService.Object);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        mockDownloadService.Verify(
            x => x.ShouldRemoveFromArrQueueAsync("torrent-download-id", It.IsAny<List<string>>()),
            Times.Once
        );
    }

    [Fact]
    public async Task ProcessInstanceAsync_WhenShouldRemove_PublishesRemoveRequest()
    {
        // Arrange
        var sonarrInstance = TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);

        var mockArrClient = new Mock<IArrClient>();
        mockArrClient.Setup(x => x.IsRecordValid(It.IsAny<QueueRecord>())).Returns(true);

        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(InstanceType.Sonarr, It.IsAny<float>()))
            .Returns(mockArrClient.Object);

        var queueRecord = new QueueRecord
        {
            Id = 1,
            DownloadId = "stalled-download-id",
            Title = "Stalled Download",
            Protocol = "torrent",
            SeriesId = 1,
            EpisodeId = 1
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

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .Setup(x => x.ShouldRemoveFromArrQueueAsync(
                It.IsAny<string>(),
                It.IsAny<List<string>>()
            ))
            .ReturnsAsync(new DownloadCheckResult
            {
                Found = true,
                ShouldRemove = true,
                IsPrivate = false,
                DeleteFromClient = true,
                DeleteReason = DeleteReason.Stalled
            });

        _fixture.DownloadServiceFactory
            .Setup(x => x.GetDownloadService(It.IsAny<DownloadClientConfig>()))
            .Returns(mockDownloadService.Object);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        _fixture.MessageBus.Verify(
            x => x.Publish(
                It.IsAny<QueueItemRemoveRequest<SeriesSearchItem>>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task ProcessInstanceAsync_WhenDownloadNotFound_LogsWarning()
    {
        // Arrange
        var sonarrInstance = TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);

        var mockArrClient = new Mock<IArrClient>();
        mockArrClient.Setup(x => x.IsRecordValid(It.IsAny<QueueRecord>())).Returns(true);
        mockArrClient.Setup(x => x.ShouldRemoveFromQueue(
            It.IsAny<InstanceType>(),
            It.IsAny<QueueRecord>(),
            It.IsAny<bool>(),
            It.IsAny<short>()
        )).ReturnsAsync(false);

        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(InstanceType.Sonarr, It.IsAny<float>()))
            .Returns(mockArrClient.Object);

        var queueRecord = new QueueRecord
        {
            Id = 1,
            DownloadId = "missing-download-id",
            Title = "Missing Download",
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

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .Setup(x => x.ShouldRemoveFromArrQueueAsync(
                It.IsAny<string>(),
                It.IsAny<List<string>>()
            ))
            .ReturnsAsync(new DownloadCheckResult { Found = false });

        _fixture.DownloadServiceFactory
            .Setup(x => x.GetDownloadService(It.IsAny<DownloadClientConfig>()))
            .Returns(mockDownloadService.Object);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        _logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Download not found in any torrent client")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task ProcessInstanceAsync_ChecksFailedImportsWhenDownloadCheckPasses()
    {
        // Arrange
        var sonarrInstance = TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);

        var mockArrClient = new Mock<IArrClient>();
        mockArrClient.Setup(x => x.IsRecordValid(It.IsAny<QueueRecord>())).Returns(true);
        mockArrClient.Setup(x => x.ShouldRemoveFromQueue(
            It.IsAny<InstanceType>(),
            It.IsAny<QueueRecord>(),
            It.IsAny<bool>(),
            It.IsAny<short>()
        )).ReturnsAsync(false);

        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(InstanceType.Sonarr, It.IsAny<float>()))
            .Returns(mockArrClient.Object);

        var queueRecord = new QueueRecord
        {
            Id = 1,
            DownloadId = "download-id",
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

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .Setup(x => x.ShouldRemoveFromArrQueueAsync(
                It.IsAny<string>(),
                It.IsAny<List<string>>()
            ))
            .ReturnsAsync(new DownloadCheckResult { Found = true, ShouldRemove = false });

        _fixture.DownloadServiceFactory
            .Setup(x => x.GetDownloadService(It.IsAny<DownloadClientConfig>()))
            .Returns(mockDownloadService.Object);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert - verify failed import check was called
        mockArrClient.Verify(
            x => x.ShouldRemoveFromQueue(
                InstanceType.Sonarr,
                queueRecord,
                false,
                It.IsAny<short>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task ProcessInstanceAsync_WhenFailedImport_PublishesRemoveRequest()
    {
        // Arrange
        var sonarrInstance = TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);

        var mockArrClient = new Mock<IArrClient>();
        mockArrClient.Setup(x => x.IsRecordValid(It.IsAny<QueueRecord>())).Returns(true);
        mockArrClient.Setup(x => x.ShouldRemoveFromQueue(
            It.IsAny<InstanceType>(),
            It.IsAny<QueueRecord>(),
            It.IsAny<bool>(),
            It.IsAny<short>()
        )).ReturnsAsync(true);

        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(InstanceType.Sonarr, It.IsAny<float>()))
            .Returns(mockArrClient.Object);

        var queueRecord = new QueueRecord
        {
            Id = 1,
            DownloadId = "failed-import-id",
            Title = "Failed Import",
            Protocol = "torrent",
            SeriesId = 1,
            EpisodeId = 1
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

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .Setup(x => x.ShouldRemoveFromArrQueueAsync(
                It.IsAny<string>(),
                It.IsAny<List<string>>()
            ))
            .ReturnsAsync(new DownloadCheckResult { Found = true, ShouldRemove = false });

        _fixture.DownloadServiceFactory
            .Setup(x => x.GetDownloadService(It.IsAny<DownloadClientConfig>()))
            .Returns(mockDownloadService.Object);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        _fixture.MessageBus.Verify(
            x => x.Publish(
                It.Is<QueueItemRemoveRequest<SeriesSearchItem>>(r =>
                    r.DeleteReason == DeleteReason.FailedImport
                ),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ProcessInstanceAsync_WhenDownloadServiceThrows_LogsErrorAndContinues()
    {
        // Arrange
        var sonarrInstance = TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);

        var mockArrClient = new Mock<IArrClient>();
        mockArrClient.Setup(x => x.IsRecordValid(It.IsAny<QueueRecord>())).Returns(true);
        mockArrClient.Setup(x => x.ShouldRemoveFromQueue(
            It.IsAny<InstanceType>(),
            It.IsAny<QueueRecord>(),
            It.IsAny<bool>(),
            It.IsAny<short>()
        )).ReturnsAsync(false);

        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(InstanceType.Sonarr, It.IsAny<float>()))
            .Returns(mockArrClient.Object);

        var queueRecord = new QueueRecord
        {
            Id = 1,
            DownloadId = "error-download-id",
            Title = "Error Download",
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

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .Setup(x => x.ShouldRemoveFromArrQueueAsync(
                It.IsAny<string>(),
                It.IsAny<List<string>>()
            ))
            .ThrowsAsync(new Exception("Connection failed"));

        _fixture.DownloadServiceFactory
            .Setup(x => x.GetDownloadService(It.IsAny<DownloadClientConfig>()))
            .Returns(mockDownloadService.Object);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        _logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error checking download")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    #endregion

    #region GenericHandler PublishQueueItemRemoveRequest Tests

    [Fact]
    public async Task PublishQueueItemRemoveRequest_WhenCacheHasKey_SkipsRemovalRequest()
    {
        // Arrange - test the cache skip in GenericHandler.PublishQueueItemRemoveRequest
        // This simulates a race condition where the key is added between QueueCleaner's check
        // and calling PublishQueueItemRemoveRequest
        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);

        var mockArrClient = new Mock<IArrClient>();
        mockArrClient.Setup(x => x.IsRecordValid(It.IsAny<QueueRecord>())).Returns(true);

        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(InstanceType.Radarr, It.IsAny<float>()))
            .Returns(mockArrClient.Object);

        var queueRecord = new QueueRecord
        {
            Id = 1,
            DownloadId = "race-condition-download",
            Title = "Race Condition Download",
            Protocol = "torrent",
            MovieId = 1
        };

        // Simulate race condition: add to cache when ShouldRemoveFromArrQueueAsync is called
        // (after QueueCleaner's cache check but before PublishQueueItemRemoveRequest)
        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .Setup(x => x.ShouldRemoveFromArrQueueAsync(
                It.IsAny<string>(),
                It.IsAny<List<string>>()
            ))
            .ReturnsAsync(() =>
            {
                // Add to cache here - simulating another thread/process adding this
                var cacheKey = CacheKeys.DownloadMarkedForRemoval(queueRecord.DownloadId, radarrInstance.Url);
                _fixture.Cache.Set(cacheKey, true);

                return new DownloadCheckResult
                {
                    Found = true,
                    ShouldRemove = true,
                    IsPrivate = false,
                    DeleteFromClient = true,
                    DeleteReason = DeleteReason.Stalled
                };
            });

        _fixture.DownloadServiceFactory
            .Setup(x => x.GetDownloadService(It.IsAny<DownloadClientConfig>()))
            .Returns(mockDownloadService.Object);

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
        await sut.ExecuteAsync();

        // Assert - should log "skip removal request | already marked for removal" from GenericHandler
        _logger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("skip removal request") && v.ToString()!.Contains("already marked for removal")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );

        // Verify no publish was made
        _fixture.MessageBus.Verify(
            x => x.Publish(
                It.IsAny<QueueItemRemoveRequest<SearchItem>>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Never
        );
    }

    [Fact]
    public async Task PublishQueueItemRemoveRequest_ForRadarr_PublishesSearchItemRequest()
    {
        // Arrange - test the SearchItem branch for Radarr (not SeriesSearchItem)
        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);

        var mockArrClient = new Mock<IArrClient>();
        mockArrClient.Setup(x => x.IsRecordValid(It.IsAny<QueueRecord>())).Returns(true);

        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(InstanceType.Radarr, It.IsAny<float>()))
            .Returns(mockArrClient.Object);

        var queueRecord = new QueueRecord
        {
            Id = 1,
            DownloadId = "radarr-download-id",
            Title = "Radarr Download",
            Protocol = "torrent",
            MovieId = 42
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

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .Setup(x => x.ShouldRemoveFromArrQueueAsync(
                It.IsAny<string>(),
                It.IsAny<List<string>>()
            ))
            .ReturnsAsync(new DownloadCheckResult
            {
                Found = true,
                ShouldRemove = true,
                IsPrivate = false,
                DeleteFromClient = true,
                DeleteReason = DeleteReason.Stalled
            });

        _fixture.DownloadServiceFactory
            .Setup(x => x.GetDownloadService(It.IsAny<DownloadClientConfig>()))
            .Returns(mockDownloadService.Object);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert - should publish QueueItemRemoveRequest<SearchItem> (not SeriesSearchItem)
        _fixture.MessageBus.Verify(
            x => x.Publish(
                It.Is<QueueItemRemoveRequest<SearchItem>>(r =>
                    r.InstanceType == InstanceType.Radarr &&
                    r.SearchItem.Id == 42 &&
                    r.DeleteReason == DeleteReason.Stalled
                ),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task PublishQueueItemRemoveRequest_ForLidarr_PublishesSearchItemRequest()
    {
        // Arrange - test the SearchItem branch for Lidarr
        var lidarrInstance = TestDataContextFactory.AddLidarrInstance(_fixture.DataContext);
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);

        var mockArrClient = new Mock<IArrClient>();
        mockArrClient.Setup(x => x.IsRecordValid(It.IsAny<QueueRecord>())).Returns(true);

        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(InstanceType.Lidarr, It.IsAny<float>()))
            .Returns(mockArrClient.Object);

        var queueRecord = new QueueRecord
        {
            Id = 1,
            DownloadId = "lidarr-download-id",
            Title = "Lidarr Download",
            Protocol = "torrent",
            AlbumId = 123
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

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .Setup(x => x.ShouldRemoveFromArrQueueAsync(
                It.IsAny<string>(),
                It.IsAny<List<string>>()
            ))
            .ReturnsAsync(new DownloadCheckResult
            {
                Found = true,
                ShouldRemove = true,
                IsPrivate = false,
                DeleteFromClient = true,
                DeleteReason = DeleteReason.SlowSpeed
            });

        _fixture.DownloadServiceFactory
            .Setup(x => x.GetDownloadService(It.IsAny<DownloadClientConfig>()))
            .Returns(mockDownloadService.Object);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert - should publish QueueItemRemoveRequest<SearchItem> with AlbumId
        _fixture.MessageBus.Verify(
            x => x.Publish(
                It.Is<QueueItemRemoveRequest<SearchItem>>(r =>
                    r.InstanceType == InstanceType.Lidarr &&
                    r.SearchItem.Id == 123 &&
                    r.DeleteReason == DeleteReason.SlowSpeed
                ),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task PublishQueueItemRemoveRequest_ForReadarr_PublishesSearchItemRequest()
    {
        // Arrange - test the SearchItem branch for Readarr
        var readarrInstance = TestDataContextFactory.AddReadarrInstance(_fixture.DataContext);
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);

        var mockArrClient = new Mock<IArrClient>();
        mockArrClient.Setup(x => x.IsRecordValid(It.IsAny<QueueRecord>())).Returns(true);

        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(InstanceType.Readarr, It.IsAny<float>()))
            .Returns(mockArrClient.Object);

        var queueRecord = new QueueRecord
        {
            Id = 1,
            DownloadId = "readarr-download-id",
            Title = "Readarr Download",
            Protocol = "torrent",
            BookId = 456
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

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .Setup(x => x.ShouldRemoveFromArrQueueAsync(
                It.IsAny<string>(),
                It.IsAny<List<string>>()
            ))
            .ReturnsAsync(new DownloadCheckResult
            {
                Found = true,
                ShouldRemove = true,
                IsPrivate = false,
                DeleteFromClient = true,
                DeleteReason = DeleteReason.Stalled
            });

        _fixture.DownloadServiceFactory
            .Setup(x => x.GetDownloadService(It.IsAny<DownloadClientConfig>()))
            .Returns(mockDownloadService.Object);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert - should publish QueueItemRemoveRequest<SearchItem> with BookId
        _fixture.MessageBus.Verify(
            x => x.Publish(
                It.Is<QueueItemRemoveRequest<SearchItem>>(r =>
                    r.InstanceType == InstanceType.Readarr &&
                    r.SearchItem.Id == 456 &&
                    r.DeleteReason == DeleteReason.Stalled
                ),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task PublishQueueItemRemoveRequest_ForWhisparrV2_PublishesSeriesSearchItemRequest()
    {
        // Arrange - test that Whisparr v2 uses SeriesSearchItem
        var whisparrInstance = TestDataContextFactory.AddWhisparrInstance(_fixture.DataContext, version: 2);
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);

        var mockArrClient = new Mock<IArrClient>();
        mockArrClient.Setup(x => x.IsRecordValid(It.IsAny<QueueRecord>())).Returns(true);

        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(InstanceType.Whisparr, 2f))
            .Returns(mockArrClient.Object);

        var queueRecord = new QueueRecord
        {
            Id = 1,
            DownloadId = "whisparr-v2-download-id",
            Title = "Whisparr V2 Download",
            Protocol = "torrent",
            SeriesId = 10,
            EpisodeId = 100
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

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .Setup(x => x.ShouldRemoveFromArrQueueAsync(
                It.IsAny<string>(),
                It.IsAny<List<string>>()
            ))
            .ReturnsAsync(new DownloadCheckResult
            {
                Found = true,
                ShouldRemove = true,
                IsPrivate = false,
                DeleteFromClient = true,
                DeleteReason = DeleteReason.Stalled
            });

        _fixture.DownloadServiceFactory
            .Setup(x => x.GetDownloadService(It.IsAny<DownloadClientConfig>()))
            .Returns(mockDownloadService.Object);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert - should publish QueueItemRemoveRequest<SeriesSearchItem>
        _fixture.MessageBus.Verify(
            x => x.Publish(
                It.Is<QueueItemRemoveRequest<SeriesSearchItem>>(r =>
                    r.InstanceType == InstanceType.Whisparr &&
                    r.SearchItem.Id == 100 && // EpisodeId
                    r.SearchItem.SeriesId == 10 &&
                    r.SearchItem.SearchType == SeriesSearchType.Episode &&
                    r.DeleteReason == DeleteReason.Stalled
                ),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task PublishQueueItemRemoveRequest_ForWhisparrV3_PublishesSearchItemRequest()
    {
        // Arrange - test that Whisparr v3 uses SearchItem
        var whisparrInstance = TestDataContextFactory.AddWhisparrInstance(_fixture.DataContext, version: 3);
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);

        var mockArrClient = new Mock<IArrClient>();
        mockArrClient.Setup(x => x.IsRecordValid(It.IsAny<QueueRecord>())).Returns(true);

        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(InstanceType.Whisparr, 3f))
            .Returns(mockArrClient.Object);

        var queueRecord = new QueueRecord
        {
            Id = 1,
            DownloadId = "whisparr-v3-download-id",
            Title = "Whisparr V3 Download",
            Protocol = "torrent",
            MovieId = 42
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

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .Setup(x => x.ShouldRemoveFromArrQueueAsync(
                It.IsAny<string>(),
                It.IsAny<List<string>>()
            ))
            .ReturnsAsync(new DownloadCheckResult
            {
                Found = true,
                ShouldRemove = true,
                IsPrivate = false,
                DeleteFromClient = true,
                DeleteReason = DeleteReason.Stalled
            });

        _fixture.DownloadServiceFactory
            .Setup(x => x.GetDownloadService(It.IsAny<DownloadClientConfig>()))
            .Returns(mockDownloadService.Object);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert - should publish QueueItemRemoveRequest<SearchItem> with MovieId
        _fixture.MessageBus.Verify(
            x => x.Publish(
                It.Is<QueueItemRemoveRequest<SearchItem>>(r =>
                    r.InstanceType == InstanceType.Whisparr &&
                    r.SearchItem.Id == 42 && // MovieId
                    r.DeleteReason == DeleteReason.Stalled
                ),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task PublishQueueItemRemoveRequest_ForWhisparrV2Pack_PublishesSeasonSearchItemRequest()
    {
        // Arrange - test that Whisparr v2 pack (multiple records with same download ID) uses SeriesSearchItem with Season search type
        var whisparrInstance = TestDataContextFactory.AddWhisparrInstance(_fixture.DataContext, version: 2);
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);

        var mockArrClient = new Mock<IArrClient>();
        mockArrClient.Setup(x => x.IsRecordValid(It.IsAny<QueueRecord>())).Returns(true);

        _fixture.ArrClientFactory
            .Setup(x => x.GetClient(InstanceType.Whisparr, 2f))
            .Returns(mockArrClient.Object);

        // Create multiple records with same download ID to simulate a pack (season pack)
        var record1 = new QueueRecord
        {
            Id = 1,
            DownloadId = "whisparr-v2-pack-download-id",
            Title = "Whisparr V2 Season Pack - Episode 1",
            Protocol = "torrent",
            SeriesId = 10,
            EpisodeId = 100,
            SeasonNumber = 3
        };
        var record2 = new QueueRecord
        {
            Id = 2,
            DownloadId = "whisparr-v2-pack-download-id",
            Title = "Whisparr V2 Season Pack - Episode 2",
            Protocol = "torrent",
            SeriesId = 10,
            EpisodeId = 101,
            SeasonNumber = 3
        };

        _fixture.ArrQueueIterator
            .Setup(x => x.Iterate(
                It.IsAny<IArrClient>(),
                It.IsAny<ArrInstance>(),
                It.IsAny<Func<IReadOnlyList<QueueRecord>, Task>>()
            ))
            .Returns(async (IArrClient client, ArrInstance instance, Func<IReadOnlyList<QueueRecord>, Task> callback) =>
            {
                await callback([record1, record2]);
            });

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .Setup(x => x.ShouldRemoveFromArrQueueAsync(
                It.IsAny<string>(),
                It.IsAny<List<string>>()
            ))
            .ReturnsAsync(new DownloadCheckResult
            {
                Found = true,
                ShouldRemove = true,
                IsPrivate = false,
                DeleteFromClient = true,
                DeleteReason = DeleteReason.Stalled
            });

        _fixture.DownloadServiceFactory
            .Setup(x => x.GetDownloadService(It.IsAny<DownloadClientConfig>()))
            .Returns(mockDownloadService.Object);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert - should publish QueueItemRemoveRequest<SeriesSearchItem> with Season search type
        // because multiple records with the same download ID indicate a pack
        _fixture.MessageBus.Verify(
            x => x.Publish(
                It.Is<QueueItemRemoveRequest<SeriesSearchItem>>(r =>
                    r.InstanceType == InstanceType.Whisparr &&
                    r.SearchItem.Id == 3 && // SeasonNumber
                    r.SearchItem.SeriesId == 10 &&
                    r.SearchItem.SearchType == SeriesSearchType.Season &&
                    r.DeleteReason == DeleteReason.Stalled
                ),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    #endregion
}
