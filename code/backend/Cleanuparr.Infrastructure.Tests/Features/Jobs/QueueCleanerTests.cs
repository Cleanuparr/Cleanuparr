using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.DownloadRemover.Models;
using Cleanuparr.Infrastructure.Helpers;
using Cleanuparr.Infrastructure.Tests.Features.Jobs.TestHelpers;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using QueueCleanerJob = Cleanuparr.Infrastructure.Features.Jobs.QueueCleaner;

namespace Cleanuparr.Infrastructure.Tests.Features.Jobs;

[Collection(JobHandlerCollection.Name)]
public class QueueCleanerTests : IDisposable
{
    private readonly JobHandlerFixture _fixture;
    private readonly ILogger<QueueCleanerJob> _logger;

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
            _logger,
            _fixture.DataContext,
            _fixture.Cache,
            _fixture.MessageBus,
            _fixture.ArrClientFactory,
            _fixture.ArrQueueIterator,
            _fixture.DownloadServiceFactory,
            _fixture.EventPublisher
        );
    }

    #region ExecuteInternalAsync Tests

    [Fact]
    public async Task ExecuteInternalAsync_LoadsStallRulesFromDatabase()
    {
        // Arrange
        TestDataContextFactory.AddStallRule(_fixture.DataContext, enabled: true);
        TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);

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
        await sut.ExecuteAsync();

        // Assert - no debug message about no active stall rules
        _logger.DidNotReceiveLogContaining(LogLevel.Debug, "No active stall rules found");
    }

    [Fact]
    public async Task ExecuteInternalAsync_WhenNoStallRules_LogsDebugMessage()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        _logger.ReceivedLogContaining(LogLevel.Debug, "No active stall rules found");
    }

    [Fact]
    public async Task ExecuteInternalAsync_LoadsSlowRulesFromDatabase()
    {
        // Arrange
        TestDataContextFactory.AddSlowRule(_fixture.DataContext, enabled: true);
        TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);

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
        await sut.ExecuteAsync();

        // Assert - no debug message about no active slow rules
        _logger.DidNotReceiveLogContaining(LogLevel.Debug, "No active slow rules found");
    }

    [Fact]
    public async Task ExecuteInternalAsync_WhenNoSlowRules_LogsDebugMessage()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        _logger.ReceivedLogContaining(LogLevel.Debug, "No active slow rules found");
    }

    [Fact]
    public async Task ExecuteInternalAsync_ProcessesAllArrConfigs()
    {
        // Arrange
        TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);
        TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

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
        await sut.ExecuteAsync();

        // Assert
        _fixture.ArrClientFactory.Received(1).GetClient(InstanceType.Sonarr, Arg.Any<float>());
        _fixture.ArrClientFactory.Received(1).GetClient(InstanceType.Radarr, Arg.Any<float>());
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

        var mockArrClient = Substitute.For<IArrClient>();
        mockArrClient.IsRecordValid(Arg.Any<QueueRecord>()).Returns(true);
        mockArrClient.HasContentId(Arg.Any<QueueRecord>()).Returns(true);

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Sonarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var queueRecord = new QueueRecord
        {
            Id = 1,
            DownloadId = "ignored-download-id",
            Title = "Ignored Download",
            Protocol = "torrent",
            SeriesId = 1,
            EpisodeId = 1
        };

        _fixture.ArrQueueIterator
            .Iterate(
                Arg.Any<IArrClient>(),
                Arg.Any<ArrInstance>(),
                Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>()
            )
            .Returns(async ci =>
            {
                var callback = ci.ArgAt<Func<IReadOnlyList<QueueRecord>, Task>>(2);
                await callback([queueRecord]);
            });

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        _logger.ReceivedLogContaining(LogLevel.Information, "download is ignored");
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

        var mockArrClient = Substitute.For<IArrClient>();
        mockArrClient.IsRecordValid(Arg.Any<QueueRecord>()).Returns(true);
        mockArrClient.HasContentId(Arg.Any<QueueRecord>()).Returns(true);

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Sonarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var queueRecord = new QueueRecord
        {
            Id = 1,
            DownloadId = "cached-download-id",
            Title = "Cached Download",
            Protocol = "torrent",
            SeriesId = 1,
            EpisodeId = 1
        };

        _fixture.ArrQueueIterator
            .Iterate(
                Arg.Any<IArrClient>(),
                Arg.Any<ArrInstance>(),
                Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>()
            )
            .Returns(async ci =>
            {
                var callback = ci.ArgAt<Func<IReadOnlyList<QueueRecord>, Task>>(2);
                await callback([queueRecord]);
            });

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        _logger.ReceivedLogContaining(LogLevel.Debug, "already marked for removal");
    }

    [Fact]
    public async Task ProcessInstanceAsync_ChecksTorrentClientsForDownloadInfo()
    {
        // Arrange
        var sonarrInstance = TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);

        var mockArrClient = Substitute.For<IArrClient>();
        mockArrClient.IsRecordValid(Arg.Any<QueueRecord>()).Returns(true);
        mockArrClient.HasContentId(Arg.Any<QueueRecord>()).Returns(true);
        mockArrClient.ShouldRemoveFromQueue(
            Arg.Any<InstanceType>(),
            Arg.Any<QueueRecord>(),
            Arg.Any<bool>(),
            Arg.Any<short>()
        ).Returns(false);

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Sonarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var queueRecord = new QueueRecord
        {
            Id = 1,
            DownloadId = "torrent-download-id",
            Title = "Torrent Download",
            Protocol = "torrent",
            SeriesId = 1,
            EpisodeId = 1
        };

        _fixture.ArrQueueIterator
            .Iterate(
                Arg.Any<IArrClient>(),
                Arg.Any<ArrInstance>(),
                Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>()
            )
            .Returns(async ci =>
            {
                var callback = ci.ArgAt<Func<IReadOnlyList<QueueRecord>, Task>>(2);
                await callback([queueRecord]);
            });

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .ShouldRemoveFromArrQueueAsync(
                Arg.Any<string>(),
                Arg.Any<List<string>>()
            )
            .Returns(new DownloadCheckResult { Found = true, ShouldRemove = false });

        _fixture.DownloadServiceFactory
            .GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        await mockDownloadService.Received(1)
            .ShouldRemoveFromArrQueueAsync("torrent-download-id", Arg.Any<List<string>>());
    }

    [Fact]
    public async Task ProcessInstanceAsync_WhenShouldRemove_PublishesRemoveRequest()
    {
        // Arrange
        var sonarrInstance = TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);

        var mockArrClient = Substitute.For<IArrClient>();
        mockArrClient.IsRecordValid(Arg.Any<QueueRecord>()).Returns(true);
        mockArrClient.HasContentId(Arg.Any<QueueRecord>()).Returns(true);

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Sonarr, Arg.Any<float>())
            .Returns(mockArrClient);

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
            .Iterate(
                Arg.Any<IArrClient>(),
                Arg.Any<ArrInstance>(),
                Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>()
            )
            .Returns(async ci =>
            {
                var callback = ci.ArgAt<Func<IReadOnlyList<QueueRecord>, Task>>(2);
                await callback([queueRecord]);
            });

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .ShouldRemoveFromArrQueueAsync(
                Arg.Any<string>(),
                Arg.Any<List<string>>()
            )
            .Returns(new DownloadCheckResult
            {
                Found = true,
                ShouldRemove = true,
                IsPrivate = false,
                DeleteFromClient = true,
                DeleteReason = DeleteReason.Stalled
            });

        _fixture.DownloadServiceFactory
            .GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        await _fixture.MessageBus.Received(1).Publish(
            Arg.Any<QueueItemRemoveRequest<SeriesSearchItem>>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task ProcessInstanceAsync_WhenDownloadNotFound_LogsWarning()
    {
        // Arrange
        var sonarrInstance = TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);

        var mockArrClient = Substitute.For<IArrClient>();
        mockArrClient.IsRecordValid(Arg.Any<QueueRecord>()).Returns(true);
        mockArrClient.HasContentId(Arg.Any<QueueRecord>()).Returns(true);
        mockArrClient.ShouldRemoveFromQueue(
            Arg.Any<InstanceType>(),
            Arg.Any<QueueRecord>(),
            Arg.Any<bool>(),
            Arg.Any<short>()
        ).Returns(false);

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Sonarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var queueRecord = new QueueRecord
        {
            Id = 1,
            DownloadId = "missing-download-id",
            Title = "Missing Download",
            Protocol = "torrent",
            SeriesId = 1,
            EpisodeId = 1
        };

        _fixture.ArrQueueIterator
            .Iterate(
                Arg.Any<IArrClient>(),
                Arg.Any<ArrInstance>(),
                Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>()
            )
            .Returns(async ci =>
            {
                var callback = ci.ArgAt<Func<IReadOnlyList<QueueRecord>, Task>>(2);
                await callback([queueRecord]);
            });

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .ShouldRemoveFromArrQueueAsync(
                Arg.Any<string>(),
                Arg.Any<List<string>>()
            )
            .Returns(new DownloadCheckResult { Found = false });

        _fixture.DownloadServiceFactory
            .GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        _logger.ReceivedLogContaining(LogLevel.Warning, "Download not found in any torrent client");
    }

    [Fact]
    public async Task ProcessInstanceAsync_ChecksFailedImportsWhenDownloadCheckPasses()
    {
        // Arrange
        var sonarrInstance = TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);

        var mockArrClient = Substitute.For<IArrClient>();
        mockArrClient.IsRecordValid(Arg.Any<QueueRecord>()).Returns(true);
        mockArrClient.HasContentId(Arg.Any<QueueRecord>()).Returns(true);
        mockArrClient.ShouldRemoveFromQueue(
            Arg.Any<InstanceType>(),
            Arg.Any<QueueRecord>(),
            Arg.Any<bool>(),
            Arg.Any<short>()
        ).Returns(false);

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Sonarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var queueRecord = new QueueRecord
        {
            Id = 1,
            DownloadId = "download-id",
            Title = "Test Download",
            Protocol = "torrent",
            SeriesId = 1,
            EpisodeId = 1
        };

        _fixture.ArrQueueIterator
            .Iterate(
                Arg.Any<IArrClient>(),
                Arg.Any<ArrInstance>(),
                Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>()
            )
            .Returns(async ci =>
            {
                var callback = ci.ArgAt<Func<IReadOnlyList<QueueRecord>, Task>>(2);
                await callback([queueRecord]);
            });

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .ShouldRemoveFromArrQueueAsync(
                Arg.Any<string>(),
                Arg.Any<List<string>>()
            )
            .Returns(new DownloadCheckResult { Found = true, ShouldRemove = false });

        _fixture.DownloadServiceFactory
            .GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert - verify failed import check was called
        await mockArrClient.Received(1).ShouldRemoveFromQueue(
            InstanceType.Sonarr,
            queueRecord,
            false,
            Arg.Any<short>()
        );
    }

    [Fact]
    public async Task ProcessInstanceAsync_WhenFailedImport_PublishesRemoveRequest()
    {
        // Arrange
        var sonarrInstance = TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);

        var mockArrClient = Substitute.For<IArrClient>();
        mockArrClient.IsRecordValid(Arg.Any<QueueRecord>()).Returns(true);
        mockArrClient.HasContentId(Arg.Any<QueueRecord>()).Returns(true);
        mockArrClient.ShouldRemoveFromQueue(
            Arg.Any<InstanceType>(),
            Arg.Any<QueueRecord>(),
            Arg.Any<bool>(),
            Arg.Any<short>()
        ).Returns(true);

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Sonarr, Arg.Any<float>())
            .Returns(mockArrClient);

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
            .Iterate(
                Arg.Any<IArrClient>(),
                Arg.Any<ArrInstance>(),
                Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>()
            )
            .Returns(async ci =>
            {
                var callback = ci.ArgAt<Func<IReadOnlyList<QueueRecord>, Task>>(2);
                await callback([queueRecord]);
            });

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .ShouldRemoveFromArrQueueAsync(
                Arg.Any<string>(),
                Arg.Any<List<string>>()
            )
            .Returns(new DownloadCheckResult { Found = true, ShouldRemove = false });

        _fixture.DownloadServiceFactory
            .GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        await _fixture.MessageBus.Received(1).Publish(
            Arg.Is<QueueItemRemoveRequest<SeriesSearchItem>>(r =>
                r.DeleteReason == DeleteReason.FailedImport
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task ProcessInstanceAsync_SkipsItem_WhenMissingContentId_AndProcessNoContentIdIsFalse()
    {
        // Arrange
        TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);

        var mockArrClient = Substitute.For<IArrClient>();
        mockArrClient.IsRecordValid(Arg.Any<QueueRecord>()).Returns(true);
        mockArrClient.HasContentId(Arg.Any<QueueRecord>()).Returns(false);

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Sonarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var queueRecord = new QueueRecord
        {
            Id = 1,
            DownloadId = "no-content-id-download",
            Title = "No Content ID Download",
            Protocol = "torrent"
        };

        _fixture.ArrQueueIterator
            .Iterate(
                Arg.Any<IArrClient>(),
                Arg.Any<ArrInstance>(),
                Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>()
            )
            .Returns(async ci =>
            {
                var callback = ci.ArgAt<Func<IReadOnlyList<QueueRecord>, Task>>(2);
                await callback([queueRecord]);
            });

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        _logger.ReceivedLogContaining(LogLevel.Information, "skip | item is missing the content id");

        await _fixture.MessageBus.DidNotReceive().Publish(
            Arg.Any<QueueItemRemoveRequest<SeriesSearchItem>>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task ProcessInstanceAsync_WhenMissingContentId_AndProcessNoContentIdIsTrue_PublishesRemoveRequestWithSkipSearch()
    {
        // Arrange
        TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);

        var queueCleanerConfig = _fixture.DataContext.QueueCleanerConfigs.First();
        queueCleanerConfig.ProcessNoContentId = true;
        _fixture.DataContext.SaveChanges();

        var mockArrClient = Substitute.For<IArrClient>();
        mockArrClient.IsRecordValid(Arg.Any<QueueRecord>()).Returns(true);
        mockArrClient.HasContentId(Arg.Any<QueueRecord>()).Returns(false);
        mockArrClient.ShouldRemoveFromQueue(
            Arg.Any<InstanceType>(),
            Arg.Any<QueueRecord>(),
            Arg.Any<bool>(),
            Arg.Any<short>()
        ).Returns(false);

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Sonarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var queueRecord = new QueueRecord
        {
            Id = 1,
            DownloadId = "no-content-id-download",
            Title = "No Content ID Download",
            Protocol = "torrent"
        };

        _fixture.ArrQueueIterator
            .Iterate(
                Arg.Any<IArrClient>(),
                Arg.Any<ArrInstance>(),
                Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>()
            )
            .Returns(async ci =>
            {
                var callback = ci.ArgAt<Func<IReadOnlyList<QueueRecord>, Task>>(2);
                await callback([queueRecord]);
            });

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .ShouldRemoveFromArrQueueAsync(
                Arg.Any<string>(),
                Arg.Any<List<string>>()
            )
            .Returns(new DownloadCheckResult
            {
                Found = true,
                ShouldRemove = true,
                IsPrivate = false,
                DeleteFromClient = true,
                DeleteReason = DeleteReason.Stalled
            });

        _fixture.DownloadServiceFactory
            .GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert - SkipSearch must be true because the item has no content ID
        await _fixture.MessageBus.Received(1).Publish(
            Arg.Is<QueueItemRemoveRequest<SeriesSearchItem>>(r =>
                r.SkipSearch == true &&
                r.DeleteReason == DeleteReason.Stalled
            ),
            Arg.Any<CancellationToken>()
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

        var mockArrClient = Substitute.For<IArrClient>();
        mockArrClient.IsRecordValid(Arg.Any<QueueRecord>()).Returns(true);
        mockArrClient.HasContentId(Arg.Any<QueueRecord>()).Returns(true);
        mockArrClient.ShouldRemoveFromQueue(
            Arg.Any<InstanceType>(),
            Arg.Any<QueueRecord>(),
            Arg.Any<bool>(),
            Arg.Any<short>()
        ).Returns(false);

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Sonarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var queueRecord = new QueueRecord
        {
            Id = 1,
            DownloadId = "error-download-id",
            Title = "Error Download",
            Protocol = "torrent",
            SeriesId = 1,
            EpisodeId = 1
        };

        _fixture.ArrQueueIterator
            .Iterate(
                Arg.Any<IArrClient>(),
                Arg.Any<ArrInstance>(),
                Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>()
            )
            .Returns(async ci =>
            {
                var callback = ci.ArgAt<Func<IReadOnlyList<QueueRecord>, Task>>(2);
                await callback([queueRecord]);
            });

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .ShouldRemoveFromArrQueueAsync(
                Arg.Any<string>(),
                Arg.Any<List<string>>()
            )
            .ThrowsAsync(new Exception("Connection failed"));

        _fixture.DownloadServiceFactory
            .GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        _logger.ReceivedLogContaining(LogLevel.Error, "Error checking download");
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

        var mockArrClient = Substitute.For<IArrClient>();
        mockArrClient.IsRecordValid(Arg.Any<QueueRecord>()).Returns(true);
        mockArrClient.HasContentId(Arg.Any<QueueRecord>()).Returns(true);

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Radarr, Arg.Any<float>())
            .Returns(mockArrClient);

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
            .ShouldRemoveFromArrQueueAsync(
                Arg.Any<string>(),
                Arg.Any<List<string>>()
            )
            .Returns(ci =>
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
            .GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        _fixture.ArrQueueIterator
            .Iterate(
                Arg.Any<IArrClient>(),
                Arg.Any<ArrInstance>(),
                Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>()
            )
            .Returns(async ci =>
            {
                var callback = ci.ArgAt<Func<IReadOnlyList<QueueRecord>, Task>>(2);
                await callback([queueRecord]);
            });

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert - should log "skip removal request | already marked for removal" from GenericHandler
        _logger.ReceivedLogContaining(LogLevel.Debug, "skip removal request");

        // Verify no publish was made
        await _fixture.MessageBus.DidNotReceive().Publish(
            Arg.Any<QueueItemRemoveRequest<SearchItem>>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task PublishQueueItemRemoveRequest_ForRadarr_PublishesSearchItemRequest()
    {
        // Arrange - test the SearchItem branch for Radarr (not SeriesSearchItem)
        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);

        var mockArrClient = Substitute.For<IArrClient>();
        mockArrClient.IsRecordValid(Arg.Any<QueueRecord>()).Returns(true);
        mockArrClient.HasContentId(Arg.Any<QueueRecord>()).Returns(true);

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Radarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var queueRecord = new QueueRecord
        {
            Id = 1,
            DownloadId = "radarr-download-id",
            Title = "Radarr Download",
            Protocol = "torrent",
            MovieId = 42
        };

        _fixture.ArrQueueIterator
            .Iterate(
                Arg.Any<IArrClient>(),
                Arg.Any<ArrInstance>(),
                Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>()
            )
            .Returns(async ci =>
            {
                var callback = ci.ArgAt<Func<IReadOnlyList<QueueRecord>, Task>>(2);
                await callback([queueRecord]);
            });

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .ShouldRemoveFromArrQueueAsync(
                Arg.Any<string>(),
                Arg.Any<List<string>>()
            )
            .Returns(new DownloadCheckResult
            {
                Found = true,
                ShouldRemove = true,
                IsPrivate = false,
                DeleteFromClient = true,
                DeleteReason = DeleteReason.Stalled
            });

        _fixture.DownloadServiceFactory
            .GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert - should publish QueueItemRemoveRequest<SearchItem> (not SeriesSearchItem)
        await _fixture.MessageBus.Received(1).Publish(
            Arg.Is<QueueItemRemoveRequest<SearchItem>>(r =>
                r.Instance.ArrConfig.Type == InstanceType.Radarr &&
                r.SearchItem.Id == 42 &&
                r.DeleteReason == DeleteReason.Stalled
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task PublishQueueItemRemoveRequest_ForLidarr_PublishesSearchItemRequest()
    {
        // Arrange - test the SearchItem branch for Lidarr
        var lidarrInstance = TestDataContextFactory.AddLidarrInstance(_fixture.DataContext);
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);

        var mockArrClient = Substitute.For<IArrClient>();
        mockArrClient.IsRecordValid(Arg.Any<QueueRecord>()).Returns(true);
        mockArrClient.HasContentId(Arg.Any<QueueRecord>()).Returns(true);

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Lidarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var queueRecord = new QueueRecord
        {
            Id = 1,
            DownloadId = "lidarr-download-id",
            Title = "Lidarr Download",
            Protocol = "torrent",
            AlbumId = 123
        };

        _fixture.ArrQueueIterator
            .Iterate(
                Arg.Any<IArrClient>(),
                Arg.Any<ArrInstance>(),
                Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>()
            )
            .Returns(async ci =>
            {
                var callback = ci.ArgAt<Func<IReadOnlyList<QueueRecord>, Task>>(2);
                await callback([queueRecord]);
            });

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .ShouldRemoveFromArrQueueAsync(
                Arg.Any<string>(),
                Arg.Any<List<string>>()
            )
            .Returns(new DownloadCheckResult
            {
                Found = true,
                ShouldRemove = true,
                IsPrivate = false,
                DeleteFromClient = true,
                DeleteReason = DeleteReason.SlowSpeed
            });

        _fixture.DownloadServiceFactory
            .GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert - should publish QueueItemRemoveRequest<SearchItem> with AlbumId
        await _fixture.MessageBus.Received(1).Publish(
            Arg.Is<QueueItemRemoveRequest<SearchItem>>(r =>
                r.Instance.ArrConfig.Type == InstanceType.Lidarr &&
                r.SearchItem.Id == 123 &&
                r.DeleteReason == DeleteReason.SlowSpeed
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task PublishQueueItemRemoveRequest_ForReadarr_PublishesSearchItemRequest()
    {
        // Arrange - test the SearchItem branch for Readarr
        var readarrInstance = TestDataContextFactory.AddReadarrInstance(_fixture.DataContext);
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);

        var mockArrClient = Substitute.For<IArrClient>();
        mockArrClient.IsRecordValid(Arg.Any<QueueRecord>()).Returns(true);
        mockArrClient.HasContentId(Arg.Any<QueueRecord>()).Returns(true);

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Readarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var queueRecord = new QueueRecord
        {
            Id = 1,
            DownloadId = "readarr-download-id",
            Title = "Readarr Download",
            Protocol = "torrent",
            BookId = 456
        };

        _fixture.ArrQueueIterator
            .Iterate(
                Arg.Any<IArrClient>(),
                Arg.Any<ArrInstance>(),
                Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>()
            )
            .Returns(async ci =>
            {
                var callback = ci.ArgAt<Func<IReadOnlyList<QueueRecord>, Task>>(2);
                await callback([queueRecord]);
            });

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .ShouldRemoveFromArrQueueAsync(
                Arg.Any<string>(),
                Arg.Any<List<string>>()
            )
            .Returns(new DownloadCheckResult
            {
                Found = true,
                ShouldRemove = true,
                IsPrivate = false,
                DeleteFromClient = true,
                DeleteReason = DeleteReason.Stalled
            });

        _fixture.DownloadServiceFactory
            .GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert - should publish QueueItemRemoveRequest<SearchItem> with BookId
        await _fixture.MessageBus.Received(1).Publish(
            Arg.Is<QueueItemRemoveRequest<SearchItem>>(r =>
                r.Instance.ArrConfig.Type == InstanceType.Readarr &&
                r.SearchItem.Id == 456 &&
                r.DeleteReason == DeleteReason.Stalled
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task PublishQueueItemRemoveRequest_ForWhisparrV2_PublishesSeriesSearchItemRequest()
    {
        // Arrange - test that Whisparr v2 uses SeriesSearchItem
        var whisparrInstance = TestDataContextFactory.AddWhisparrInstance(_fixture.DataContext, version: 2);
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);

        var mockArrClient = Substitute.For<IArrClient>();
        mockArrClient.IsRecordValid(Arg.Any<QueueRecord>()).Returns(true);
        mockArrClient.HasContentId(Arg.Any<QueueRecord>()).Returns(true);

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Whisparr, 2f)
            .Returns(mockArrClient);

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
            .Iterate(
                Arg.Any<IArrClient>(),
                Arg.Any<ArrInstance>(),
                Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>()
            )
            .Returns(async ci =>
            {
                var callback = ci.ArgAt<Func<IReadOnlyList<QueueRecord>, Task>>(2);
                await callback([queueRecord]);
            });

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .ShouldRemoveFromArrQueueAsync(
                Arg.Any<string>(),
                Arg.Any<List<string>>()
            )
            .Returns(new DownloadCheckResult
            {
                Found = true,
                ShouldRemove = true,
                IsPrivate = false,
                DeleteFromClient = true,
                DeleteReason = DeleteReason.Stalled
            });

        _fixture.DownloadServiceFactory
            .GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert - should publish QueueItemRemoveRequest<SeriesSearchItem>
        await _fixture.MessageBus.Received(1).Publish(
            Arg.Is<QueueItemRemoveRequest<SeriesSearchItem>>(r =>
                r.Instance.ArrConfig.Type == InstanceType.Whisparr &&
                r.SearchItem.Id == 100 && // EpisodeId
                r.SearchItem.SeriesId == 10 &&
                r.SearchItem.SearchType == SeriesSearchType.Episode &&
                r.DeleteReason == DeleteReason.Stalled
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task PublishQueueItemRemoveRequest_ForWhisparrV3_PublishesSearchItemRequest()
    {
        // Arrange - test that Whisparr v3 uses SearchItem
        var whisparrInstance = TestDataContextFactory.AddWhisparrInstance(_fixture.DataContext, version: 3);
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);

        var mockArrClient = Substitute.For<IArrClient>();
        mockArrClient.IsRecordValid(Arg.Any<QueueRecord>()).Returns(true);
        mockArrClient.HasContentId(Arg.Any<QueueRecord>()).Returns(true);

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Whisparr, 3f)
            .Returns(mockArrClient);

        var queueRecord = new QueueRecord
        {
            Id = 1,
            DownloadId = "whisparr-v3-download-id",
            Title = "Whisparr V3 Download",
            Protocol = "torrent",
            MovieId = 42
        };

        _fixture.ArrQueueIterator
            .Iterate(
                Arg.Any<IArrClient>(),
                Arg.Any<ArrInstance>(),
                Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>()
            )
            .Returns(async ci =>
            {
                var callback = ci.ArgAt<Func<IReadOnlyList<QueueRecord>, Task>>(2);
                await callback([queueRecord]);
            });

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .ShouldRemoveFromArrQueueAsync(
                Arg.Any<string>(),
                Arg.Any<List<string>>()
            )
            .Returns(new DownloadCheckResult
            {
                Found = true,
                ShouldRemove = true,
                IsPrivate = false,
                DeleteFromClient = true,
                DeleteReason = DeleteReason.Stalled
            });

        _fixture.DownloadServiceFactory
            .GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert - should publish QueueItemRemoveRequest<SearchItem> with MovieId
        await _fixture.MessageBus.Received(1).Publish(
            Arg.Is<QueueItemRemoveRequest<SearchItem>>(r =>
                r.Instance.ArrConfig.Type == InstanceType.Whisparr &&
                r.SearchItem.Id == 42 && // MovieId
                r.DeleteReason == DeleteReason.Stalled
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task PublishQueueItemRemoveRequest_ForWhisparrV2Pack_PublishesSeasonSearchItemRequest()
    {
        // Arrange - test that Whisparr v2 pack (multiple records with same download ID) uses SeriesSearchItem with Season search type
        var whisparrInstance = TestDataContextFactory.AddWhisparrInstance(_fixture.DataContext, version: 2);
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);

        var mockArrClient = Substitute.For<IArrClient>();
        mockArrClient.IsRecordValid(Arg.Any<QueueRecord>()).Returns(true);
        mockArrClient.HasContentId(Arg.Any<QueueRecord>()).Returns(true);

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Whisparr, 2f)
            .Returns(mockArrClient);

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
            .Iterate(
                Arg.Any<IArrClient>(),
                Arg.Any<ArrInstance>(),
                Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>()
            )
            .Returns(async ci =>
            {
                var callback = ci.ArgAt<Func<IReadOnlyList<QueueRecord>, Task>>(2);
                await callback([record1, record2]);
            });

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .ShouldRemoveFromArrQueueAsync(
                Arg.Any<string>(),
                Arg.Any<List<string>>()
            )
            .Returns(new DownloadCheckResult
            {
                Found = true,
                ShouldRemove = true,
                IsPrivate = false,
                DeleteFromClient = true,
                DeleteReason = DeleteReason.Stalled
            });

        _fixture.DownloadServiceFactory
            .GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert - should publish QueueItemRemoveRequest<SeriesSearchItem> with Season search type
        // because multiple records with the same download ID indicate a pack
        await _fixture.MessageBus.Received(1).Publish(
            Arg.Is<QueueItemRemoveRequest<SeriesSearchItem>>(r =>
                r.Instance.ArrConfig.Type == InstanceType.Whisparr &&
                r.SearchItem.Id == 3 && // SeasonNumber
                r.SearchItem.SeriesId == 10 &&
                r.SearchItem.SearchType == SeriesSearchType.Season &&
                r.DeleteReason == DeleteReason.Stalled
            ),
            Arg.Any<CancellationToken>()
        );
    }

    #endregion

    #region ChangeCategory Tests

    [Fact]
    public async Task ProcessInstanceAsync_WhenFailedImportWithChangeCategory_PublishesRequestWithChangeCategoryAndRemoveFromClientFalse()
    {
        // Arrange
        TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);

        var queueCleanerConfig = _fixture.DataContext.QueueCleanerConfigs.First();
        queueCleanerConfig.FailedImport = queueCleanerConfig.FailedImport with { ChangeCategory = true };
        _fixture.DataContext.SaveChanges();

        var mockArrClient = Substitute.For<IArrClient>();
        mockArrClient.IsRecordValid(Arg.Any<QueueRecord>()).Returns(true);
        mockArrClient.HasContentId(Arg.Any<QueueRecord>()).Returns(true);
        mockArrClient.ShouldRemoveFromQueue(
            Arg.Any<InstanceType>(),
            Arg.Any<QueueRecord>(),
            Arg.Any<bool>(),
            Arg.Any<short>()
        ).Returns(true);

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Sonarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var queueRecord = new QueueRecord
        {
            Id = 1,
            DownloadId = "failed-import-change-category",
            Title = "Failed Import Change Category",
            Protocol = "torrent",
            SeriesId = 1,
            EpisodeId = 1
        };

        _fixture.ArrQueueIterator
            .Iterate(
                Arg.Any<IArrClient>(),
                Arg.Any<ArrInstance>(),
                Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>()
            )
            .Returns(async ci =>
            {
                var callback = ci.ArgAt<Func<IReadOnlyList<QueueRecord>, Task>>(2);
                await callback([queueRecord]);
            });

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .ShouldRemoveFromArrQueueAsync(
                Arg.Any<string>(),
                Arg.Any<List<string>>()
            )
            .Returns(new DownloadCheckResult { Found = true, ShouldRemove = false, IsPrivate = true });

        _fixture.DownloadServiceFactory
            .GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        await _fixture.MessageBus.Received(1).Publish(
            Arg.Is<QueueItemRemoveRequest<SeriesSearchItem>>(r =>
                r.DeleteReason == DeleteReason.FailedImport &&
                r.ChangeCategory == true &&
                r.RemoveFromClient == false
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task ProcessInstanceAsync_WhenStallRuleHasChangeCategory_PublishesRequestWithChangeCategoryAndRemoveFromClientFalse()
    {
        // Arrange
        TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);

        var mockArrClient = Substitute.For<IArrClient>();
        mockArrClient.IsRecordValid(Arg.Any<QueueRecord>()).Returns(true);
        mockArrClient.HasContentId(Arg.Any<QueueRecord>()).Returns(true);

        _fixture.ArrClientFactory
            .GetClient(InstanceType.Sonarr, Arg.Any<float>())
            .Returns(mockArrClient);

        var queueRecord = new QueueRecord
        {
            Id = 1,
            DownloadId = "stall-change-category",
            Title = "Stall Change Category",
            Protocol = "torrent",
            SeriesId = 1,
            EpisodeId = 1
        };

        _fixture.ArrQueueIterator
            .Iterate(
                Arg.Any<IArrClient>(),
                Arg.Any<ArrInstance>(),
                Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>()
            )
            .Returns(async ci =>
            {
                var callback = ci.ArgAt<Func<IReadOnlyList<QueueRecord>, Task>>(2);
                await callback([queueRecord]);
            });

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService
            .ShouldRemoveFromArrQueueAsync(
                Arg.Any<string>(),
                Arg.Any<List<string>>()
            )
            .Returns(new DownloadCheckResult
            {
                Found = true,
                ShouldRemove = true,
                IsPrivate = true,
                DeleteFromClient = true,
                ChangeCategory = true,
                DeleteReason = DeleteReason.Stalled,
            });

        _fixture.DownloadServiceFactory
            .GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        await _fixture.MessageBus.Received(1).Publish(
            Arg.Is<QueueItemRemoveRequest<SeriesSearchItem>>(r =>
                r.DeleteReason == DeleteReason.Stalled &&
                r.ChangeCategory == true &&
                r.RemoveFromClient == false
            ),
            Arg.Any<CancellationToken>()
        );
    }

    #endregion
}
