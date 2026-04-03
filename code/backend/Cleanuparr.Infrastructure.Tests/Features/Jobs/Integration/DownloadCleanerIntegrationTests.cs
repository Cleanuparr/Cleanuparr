using System.Text.Json;
using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Features.Jobs;
using Cleanuparr.Infrastructure.Tests.Features.Jobs.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Jobs.Integration;

[Collection(IntegrationTestCollection.Name)]
public class DownloadCleanerIntegrationTests : IDisposable
{
    private readonly IntegrationTestFixture _fixture;

    public DownloadCleanerIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    public void Dispose()
    {
        Striker.RecurringHashes.Clear();
    }

    private DownloadCleaner CreateSut()
    {
        return new DownloadCleaner(
            Substitute.For<ILogger<DownloadCleaner>>(),
            _fixture.DataContext,
            _fixture.Cache,
            _fixture.MessageBus,
            _fixture.ArrClientFactory,
            _fixture.ArrQueueIterator,
            _fixture.DownloadServiceFactory,
            _fixture.EventPublisher,
            _fixture.TimeProvider,
            _fixture.HardLinkFileService);
    }

    /// <summary>
    /// Creates a mock download service that uses the actual DB config (so seeding rules match by ID).
    /// </summary>
    private static IDownloadService CreateMockDownloadServiceWithDbConfig(DownloadClientConfig dbConfig)
    {
        var mock = Substitute.For<IDownloadService>();
        mock.ClientConfig.Returns(dbConfig);
        mock.LoginAsync().Returns(Task.CompletedTask);
        return mock;
    }

    [Fact]
    public async Task ArrManagedDownloads_AreExcludedFromCleanup()
    {
        // Arrange
        TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);
        var downloadClient = TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        TestDataContextFactory.AddSeedingRule(_fixture.DataContext);

        string arrManagedHash = "arr_managed_hash_123";
        string orphanedHash = "orphaned_hash_456";

        var arrManagedDownload = CreateMockTorrentItem(arrManagedHash, "Managed.Show.S01E01");
        var orphanedDownload = CreateMockTorrentItem(orphanedHash, "Orphaned.Movie.2024");

        var mockDownloadService = CreateMockDownloadServiceWithDbConfig(downloadClient);
        mockDownloadService.GetSeedingDownloads()
            .Returns([arrManagedDownload, orphanedDownload]);

        _fixture.DownloadServiceFactory.GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        // Setup arr queue iterator to return the arr-managed hash
        var queueRecord = new QueueRecord
        {
            Id = 1,
            Title = "Managed.Show.S01E01",
            Protocol = "torrent",
            DownloadId = arrManagedHash
        };
        _fixture.SetupArrQueueIterator(queueRecord);

        var sut = CreateSut();

        // Act - advance time past the 10s delay
        var executeTask = sut.ExecuteAsync();
        _fixture.TimeProvider.Advance(TimeSpan.FromSeconds(15));
        await executeTask;

        // Assert: Only the orphaned download should be passed to filter/clean
        mockDownloadService.Received().FilterDownloadsToBeCleanedAsync(
            Arg.Is<List<ITorrentItemWrapper>>(list =>
                list.Count == 1 && list[0].Hash == orphanedHash),
            Arg.Any<List<ISeedingRule>>());
    }

    [Fact]
    public async Task IgnoredDownloads_AreExcludedFromCleanup()
    {
        // Arrange
        TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);
        var downloadClient = TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        TestDataContextFactory.AddSeedingRule(_fixture.DataContext);

        // Add a download name to the ignored list
        var generalConfig = await _fixture.DataContext.GeneralConfigs.FirstAsync();
        generalConfig.IgnoredDownloads.Add("ignored_download");
        await _fixture.DataContext.SaveChangesAsync();

        var ignoredDownload = CreateMockTorrentItem("some_hash", "ignored_download");
        var normalDownload = CreateMockTorrentItem("normal_hash", "Normal.Movie.2024");

        var mockDownloadService = CreateMockDownloadServiceWithDbConfig(downloadClient);
        mockDownloadService.GetSeedingDownloads()
            .Returns([ignoredDownload, normalDownload]);

        _fixture.DownloadServiceFactory.GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        // No arr-managed downloads
        _fixture.ArrQueueIterator.Iterate(
                Arg.Any<IArrClient>(), Arg.Any<ArrInstance>(),
                Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>())
            .Returns(ci =>
            {
                var callback = ci.Arg<Func<IReadOnlyList<QueueRecord>, Task>>();
                return callback(Array.Empty<QueueRecord>());
            });

        var sut = CreateSut();

        // Act
        var executeTask = sut.ExecuteAsync();
        _fixture.TimeProvider.Advance(TimeSpan.FromSeconds(15));
        await executeTask;

        // Assert: Only the non-ignored download should be processed
        mockDownloadService.Received().FilterDownloadsToBeCleanedAsync(
            Arg.Is<List<ITorrentItemWrapper>>(list =>
                list.Count == 1 && list[0].Hash == "normal_hash"),
            Arg.Any<List<ISeedingRule>>());
    }

    [Fact]
    public async Task NoDownloadClients_ExitsEarly()
    {
        // Arrange: No download clients configured (default seed has none)
        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert: No download service interactions, no events
        _fixture.DownloadServiceFactory.DidNotReceive()
            .GetDownloadService(Arg.Any<DownloadClientConfig>());
        var events = await _fixture.EventsContext.Events.ToListAsync();
        events.ShouldBeEmpty();
    }

    [Fact]
    public async Task CleanedDownload_PublishesDownloadCleanedEvent()
    {
        // Arrange
        TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);
        var downloadClient = TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        TestDataContextFactory.AddSeedingRule(_fixture.DataContext);

        var torrent = CreateMockTorrentItem("cleaned_hash_abc", "Completed.Movie.2024");

        var mockDownloadService = CreateMockDownloadServiceWithDbConfig(downloadClient);
        mockDownloadService.GetSeedingDownloads().Returns([torrent]);
        mockDownloadService.FilterDownloadsToBeCleanedAsync(
            Arg.Any<List<ITorrentItemWrapper>>(), Arg.Any<List<ISeedingRule>>())
            .Returns(ci => ci.Arg<List<ITorrentItemWrapper>>());

        // Configure CleanDownloadsAsync to simulate what real DownloadService does:
        // set ContextProvider keys and call real EventPublisher
        mockDownloadService.CleanDownloadsAsync(
            Arg.Any<List<ITorrentItemWrapper>>(), Arg.Any<List<ISeedingRule>>())
            .Returns(async ci =>
            {
                ContextProvider.Set(ContextProvider.Keys.ItemName, "Completed.Movie.2024");
                ContextProvider.Set(ContextProvider.Keys.Hash, "cleaned_hash_abc");
                ContextProvider.Set(ContextProvider.Keys.DownloadClientUrl, downloadClient.ExternalOrInternalUrl);
                ContextProvider.Set(ContextProvider.Keys.DownloadClientId, downloadClient.Id);
                ContextProvider.Set(ContextProvider.Keys.DownloadClientType, downloadClient.TypeName);
                ContextProvider.Set(ContextProvider.Keys.DownloadClientName, downloadClient.Name);
                await _fixture.EventPublisher.PublishDownloadCleaned(
                    1.5, TimeSpan.FromHours(24), "completed", CleanReason.MaxRatioReached);
            });

        _fixture.DownloadServiceFactory.GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        // No arr-managed downloads
        _fixture.ArrQueueIterator.Iterate(
                Arg.Any<IArrClient>(), Arg.Any<ArrInstance>(),
                Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>())
            .Returns(ci =>
            {
                var callback = ci.Arg<Func<IReadOnlyList<QueueRecord>, Task>>();
                return callback(Array.Empty<QueueRecord>());
            });

        var sut = CreateSut();

        // Act
        var executeTask = sut.ExecuteAsync();
        _fixture.TimeProvider.Advance(TimeSpan.FromSeconds(15));
        await executeTask;

        // Assert: Full DownloadCleaned event property verification
        var events = await _fixture.EventsContext.Events.ToListAsync();
        events.Count.ShouldBe(1);

        var cleanedEvent = events.First(e => e.EventType == EventType.DownloadCleaned);
        cleanedEvent.Message.ShouldBe("Cleaned item from download client with reason: MaxRatioReached");
        cleanedEvent.Severity.ShouldBe(EventSeverity.Important);
        cleanedEvent.JobRunId.ShouldBe(_fixture.JobRunId);
        cleanedEvent.ArrInstanceId.ShouldBeNull();
        cleanedEvent.DownloadClientId.ShouldBe(downloadClient.Id);
        cleanedEvent.IsDryRun.ShouldBe(false);
        cleanedEvent.StrikeId.ShouldBeNull();
        cleanedEvent.TrackingId.ShouldBeNull();
        cleanedEvent.SearchStatus.ShouldBeNull();
        cleanedEvent.CompletedAt.ShouldBeNull();
        cleanedEvent.CycleId.ShouldBeNull();
        cleanedEvent.Data.ShouldNotBeNull();
        using (var data = JsonDocument.Parse(cleanedEvent.Data!))
        {
            data.RootElement.GetProperty("itemName").GetString().ShouldBe("Completed.Movie.2024");
            data.RootElement.GetProperty("hash").GetString().ShouldBe("cleaned_hash_abc");
            data.RootElement.GetProperty("categoryName").GetString().ShouldBe("completed");
            data.RootElement.GetProperty("ratio").GetDouble().ShouldBe(1.5);
            data.RootElement.GetProperty("seedingTime").GetDouble().ShouldBe(24.0);
            data.RootElement.GetProperty("reason").GetString().ShouldBe("MaxRatioReached");
        }

        // Assert: Notification sent
        await _fixture.NotificationPublisher.Received(1)
            .NotifyDownloadCleaned(1.5, TimeSpan.FromHours(24), "completed", CleanReason.MaxRatioReached);
    }

    [Fact]
    public async Task UnlinkedDownload_PublishesCategoryChangedEvent()
    {
        // Arrange
        TestDataContextFactory.AddSonarrInstance(_fixture.DataContext);
        var downloadClient = TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        TestDataContextFactory.AddUnlinkedConfig(_fixture.DataContext,
            enabled: true, targetCategory: "unlinked", categories: ["completed"]);

        var torrent = CreateMockTorrentItem("unlinked_hash_xyz", "NoLinks.Movie.2024", category: "completed");

        var mockDownloadService = CreateMockDownloadServiceWithDbConfig(downloadClient);
        mockDownloadService.GetSeedingDownloads().Returns([torrent]);
        mockDownloadService.FilterDownloadsToChangeCategoryAsync(
            Arg.Any<List<ITorrentItemWrapper>>(), Arg.Any<UnlinkedConfig>())
            .Returns(ci => ci.Arg<List<ITorrentItemWrapper>>());

        // Configure ChangeCategoryForNoHardLinksAsync to simulate what real DownloadService does
        mockDownloadService.ChangeCategoryForNoHardLinksAsync(
            Arg.Any<List<ITorrentItemWrapper>>(), Arg.Any<UnlinkedConfig>())
            .Returns(async ci =>
            {
                ContextProvider.Set(ContextProvider.Keys.ItemName, "NoLinks.Movie.2024");
                ContextProvider.Set(ContextProvider.Keys.Hash, "unlinked_hash_xyz");
                ContextProvider.Set(ContextProvider.Keys.DownloadClientUrl, downloadClient.ExternalOrInternalUrl);
                ContextProvider.Set(ContextProvider.Keys.DownloadClientId, downloadClient.Id);
                ContextProvider.Set(ContextProvider.Keys.DownloadClientType, downloadClient.TypeName);
                ContextProvider.Set(ContextProvider.Keys.DownloadClientName, downloadClient.Name);
                await _fixture.EventPublisher.PublishCategoryChanged("completed", "unlinked");
            });

        _fixture.DownloadServiceFactory.GetDownloadService(Arg.Any<DownloadClientConfig>())
            .Returns(mockDownloadService);

        // No arr-managed downloads
        _fixture.ArrQueueIterator.Iterate(
                Arg.Any<IArrClient>(), Arg.Any<ArrInstance>(),
                Arg.Any<Func<IReadOnlyList<QueueRecord>, Task>>())
            .Returns(ci =>
            {
                var callback = ci.Arg<Func<IReadOnlyList<QueueRecord>, Task>>();
                return callback(Array.Empty<QueueRecord>());
            });

        var sut = CreateSut();

        // Act
        var executeTask = sut.ExecuteAsync();
        _fixture.TimeProvider.Advance(TimeSpan.FromSeconds(15));
        await executeTask;

        // Assert: Full CategoryChanged event property verification
        var events = await _fixture.EventsContext.Events.ToListAsync();
        events.Count.ShouldBe(1);

        var categoryEvent = events.First(e => e.EventType == EventType.CategoryChanged);
        categoryEvent.Message.ShouldBe("Category changed from 'completed' to 'unlinked'");
        categoryEvent.Severity.ShouldBe(EventSeverity.Information);
        categoryEvent.JobRunId.ShouldBe(_fixture.JobRunId);
        categoryEvent.ArrInstanceId.ShouldBeNull();
        categoryEvent.DownloadClientId.ShouldBe(downloadClient.Id);
        categoryEvent.IsDryRun.ShouldBe(false);
        categoryEvent.StrikeId.ShouldBeNull();
        categoryEvent.TrackingId.ShouldBeNull();
        categoryEvent.SearchStatus.ShouldBeNull();
        categoryEvent.CompletedAt.ShouldBeNull();
        categoryEvent.CycleId.ShouldBeNull();
        categoryEvent.Data.ShouldNotBeNull();
        using (var data = JsonDocument.Parse(categoryEvent.Data!))
        {
            data.RootElement.GetProperty("itemName").GetString().ShouldBe("NoLinks.Movie.2024");
            data.RootElement.GetProperty("hash").GetString().ShouldBe("unlinked_hash_xyz");
            data.RootElement.GetProperty("oldCategory").GetString().ShouldBe("completed");
            data.RootElement.GetProperty("newCategory").GetString().ShouldBe("unlinked");
            data.RootElement.GetProperty("isTag").GetBoolean().ShouldBe(false);
        }

        // Assert: Notification sent
        await _fixture.NotificationPublisher.Received(1)
            .NotifyCategoryChanged("completed", "unlinked", false);
    }

    private static ITorrentItemWrapper CreateMockTorrentItem(string hash, string name, string? category = null)
    {
        var mock = Substitute.For<ITorrentItemWrapper>();
        mock.Hash.Returns(hash);
        mock.Name.Returns(name);
        mock.Category.Returns(category);
        mock.IsIgnored(Arg.Any<List<string>>()).Returns(ci =>
        {
            var ignoredList = ci.Arg<List<string>>();
            return ignoredList.Contains(name, StringComparer.InvariantCultureIgnoreCase);
        });
        return mock;
    }
}
