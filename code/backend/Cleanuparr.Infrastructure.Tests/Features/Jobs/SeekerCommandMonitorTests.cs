using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events.Interfaces;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.Jobs;
using Cleanuparr.Infrastructure.Tests.Features.Jobs.TestHelpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Jobs;

public class SeekerCommandMonitorTests : IAsyncDisposable
{
    private readonly DataContext _dataContext;
    private readonly FakeTimeProvider _timeProvider;
    private readonly IArrClient _arrClient;
    private readonly IEventPublisher _eventPublisher;
    private readonly SeekerCommandMonitor _sut;
    private readonly CancellationTokenSource _cts;

    public SeekerCommandMonitorTests()
    {
        _dataContext = TestDataContextFactory.Create();
        _timeProvider = new FakeTimeProvider();
        _arrClient = Substitute.For<IArrClient>();
        _eventPublisher = Substitute.For<IEventPublisher>();
        _cts = new CancellationTokenSource();

        var logger = Substitute.For<ILogger<SeekerCommandMonitor>>();
        var arrClientFactory = Substitute.For<IArrClientFactory>();

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(DataContext)).Returns(_dataContext);
        serviceProvider.GetService(typeof(IArrClientFactory)).Returns(arrClientFactory);
        serviceProvider.GetService(typeof(IEventPublisher)).Returns(_eventPublisher);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        arrClientFactory.GetClient(Arg.Any<InstanceType>(), Arg.Any<float>()).Returns(_arrClient);

        _sut = new SeekerCommandMonitor(logger, scopeFactory, _timeProvider);
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        try { await _sut.StopAsync(CancellationToken.None); }
        catch { /* expected during teardown */ }
        _sut.Dispose();
        _dataContext.Dispose();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Deduplicates_grabbed_items_by_download_id_for_sonarr_season_packs()
    {
        // Arrange
        var sonarrInstance = TestDataContextFactory.AddSonarrInstance(_dataContext);
        var eventId = Guid.NewGuid();

        _dataContext.SeekerCommandTrackers.Add(new SeekerCommandTracker
        {
            ArrInstanceId = sonarrInstance.Id,
            CommandId = 1,
            EventId = eventId,
            ExternalItemId = 100,
            ItemTitle = "Test Series - Season 1",
            SeasonNumber = 1,
            Status = SearchCommandStatus.Pending,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        });
        await _dataContext.SaveChangesAsync();

        _arrClient.GetCommandStatusAsync(Arg.Any<ArrInstance>(), Arg.Any<long>())
            .Returns(new ArrCommandStatus(1, "completed", null));

        // 3 episodes from same season pack share the same DownloadId
        _arrClient.GetQueueItemsAsync(Arg.Any<ArrInstance>(), Arg.Any<int>())
            .Returns(new QueueListResponse
            {
                TotalRecords = 3,
                Records =
                [
                    new QueueRecord { Id = 1, SeriesId = 100, SeasonNumber = 1, Title = "Test.Series.S01.1080p", DownloadId = "ABC123", Protocol = "torrent", Status = "downloading" },
                    new QueueRecord { Id = 2, SeriesId = 100, SeasonNumber = 1, Title = "Test.Series.S01.1080p", DownloadId = "ABC123", Protocol = "torrent", Status = "downloading" },
                    new QueueRecord { Id = 3, SeriesId = 100, SeasonNumber = 1, Title = "Test.Series.S01.1080p", DownloadId = "ABC123", Protocol = "torrent", Status = "downloading" },
                ]
            });

        var publishTcs = new TaskCompletionSource<List<string>?>();
        _eventPublisher.PublishSearchCompleted(
                Arg.Any<Guid>(), Arg.Any<SearchCommandStatus>(), Arg.Any<InstanceType>(), Arg.Any<string>(), Arg.Any<List<string>?>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => publishTcs.TrySetResult(ci.ArgAt<List<string>?>(4)));

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        var resultData = await publishTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        await _eventPublisher.Received(1).PublishSearchCompleted(
            eventId, SearchCommandStatus.Completed, Arg.Any<InstanceType>(), Arg.Any<string>(), Arg.Any<List<string>?>());

        resultData.ShouldNotBeNull();
        resultData!.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Filters_out_records_with_empty_download_id()
    {
        // Arrange
        var sonarrInstance = TestDataContextFactory.AddSonarrInstance(_dataContext);
        var eventId = Guid.NewGuid();

        _dataContext.SeekerCommandTrackers.Add(new SeekerCommandTracker
        {
            ArrInstanceId = sonarrInstance.Id,
            CommandId = 1,
            EventId = eventId,
            ExternalItemId = 100,
            ItemTitle = "Test Series - Season 1",
            SeasonNumber = 1,
            Status = SearchCommandStatus.Pending,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        });
        await _dataContext.SaveChangesAsync();

        _arrClient.GetCommandStatusAsync(Arg.Any<ArrInstance>(), Arg.Any<long>())
            .Returns(new ArrCommandStatus(1, "completed", null));

        // Queue has records with empty DownloadId and one valid record
        _arrClient.GetQueueItemsAsync(Arg.Any<ArrInstance>(), Arg.Any<int>())
            .Returns(new QueueListResponse
            {
                TotalRecords = 3,
                Records =
                [
                    new QueueRecord { Id = 1, SeriesId = 100, SeasonNumber = 1, Title = "Empty DL 1", DownloadId = "", Protocol = "torrent", Status = "downloading" },
                    new QueueRecord { Id = 2, SeriesId = 100, SeasonNumber = 1, Title = "Empty DL 2", DownloadId = "", Protocol = "torrent", Status = "downloading" },
                    new QueueRecord { Id = 3, SeriesId = 100, SeasonNumber = 1, Title = "Valid Download", DownloadId = "VALID123", Protocol = "torrent", Status = "downloading" },
                ]
            });

        var publishTcs = new TaskCompletionSource<List<string>?>();
        _eventPublisher.PublishSearchCompleted(
                Arg.Any<Guid>(), Arg.Any<SearchCommandStatus>(), Arg.Any<InstanceType>(), Arg.Any<string>(), Arg.Any<List<string>?>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => publishTcs.TrySetResult(ci.ArgAt<List<string>?>(4)));

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        var resultData = await publishTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        resultData.ShouldNotBeNull();
        resultData!.Count.ShouldBe(1);
        resultData[0].ShouldBe("Valid Download");
    }

    [Fact]
    public async Task Reports_multiple_grabbed_items_with_different_download_ids()
    {
        // Arrange
        var radarrInstance = TestDataContextFactory.AddRadarrInstance(_dataContext);
        var eventId = Guid.NewGuid();

        _dataContext.SeekerCommandTrackers.Add(new SeekerCommandTracker
        {
            ArrInstanceId = radarrInstance.Id,
            CommandId = 1,
            EventId = eventId,
            ExternalItemId = 200,
            ItemTitle = "Test Movie",
            SeasonNumber = 0,
            Status = SearchCommandStatus.Pending,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        });
        await _dataContext.SaveChangesAsync();

        _arrClient.GetCommandStatusAsync(Arg.Any<ArrInstance>(), Arg.Any<long>())
            .Returns(new ArrCommandStatus(1, "completed", null));

        // Two different downloads for the same movie
        _arrClient.GetQueueItemsAsync(Arg.Any<ArrInstance>(), Arg.Any<int>())
            .Returns(new QueueListResponse
            {
                TotalRecords = 2,
                Records =
                [
                    new QueueRecord { Id = 1, MovieId = 200, Title = "Test.Movie.720p", DownloadId = "HASH1", Protocol = "torrent", Status = "downloading" },
                    new QueueRecord { Id = 2, MovieId = 200, Title = "Test.Movie.1080p", DownloadId = "HASH2", Protocol = "usenet", Status = "downloading" },
                ]
            });

        var publishTcs = new TaskCompletionSource<List<string>?>();
        _eventPublisher.PublishSearchCompleted(
                Arg.Any<Guid>(), Arg.Any<SearchCommandStatus>(), Arg.Any<InstanceType>(), Arg.Any<string>(), Arg.Any<List<string>?>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => publishTcs.TrySetResult(ci.ArgAt<List<string>?>(4)));

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        var resultData = await publishTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        resultData.ShouldNotBeNull();
        resultData!.Count.ShouldBe(2);
    }

}
