using System.Net;
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
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Jobs;

public class SeekerCommandMonitorTests : IAsyncDisposable
{
    private readonly DataContext _dataContext;
    private readonly EventsContext _eventsContext;
    private readonly FakeTimeProvider _timeProvider;
    private readonly IArrClient _arrClient;
    private readonly IEventPublisher _eventPublisher;
    private readonly SeekerCommandMonitor _sut;
    private readonly CancellationTokenSource _cts;

    public SeekerCommandMonitorTests()
    {
        _dataContext = TestDataContextFactory.Create();
        _eventsContext = TestDataContextFactory.CreateEvents();
        _timeProvider = new FakeTimeProvider();
        _arrClient = Substitute.For<IArrClient>();
        _eventPublisher = Substitute.For<IEventPublisher>();
        _cts = new CancellationTokenSource();

        var logger = Substitute.For<ILogger<SeekerCommandMonitor>>();
        var arrClientFactory = Substitute.For<IArrClientFactory>();

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(DataContext)).Returns(_dataContext);
        serviceProvider.GetService(typeof(EventsContext)).Returns(_eventsContext);
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
        _eventsContext.Dispose();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Deduplicates_grabbed_items_by_download_id_for_sonarr_season_packs()
    {
        // Arrange
        var sonarrInstance = TestDataContextFactory.AddSonarrInstance(_dataContext);
        var eventId = Guid.NewGuid();

        _eventsContext.SeekerCommandTrackers.Add(new SeekerCommandTracker
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
        await _eventsContext.SaveChangesAsync();

        _arrClient.GetCommandStatusAsync(Arg.Any<ArrInstance>(), Arg.Any<long>())
            .Returns(new ArrCommandStatus(1, ArrCommandState.Completed, null));

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

        _eventsContext.SeekerCommandTrackers.Add(new SeekerCommandTracker
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
        await _eventsContext.SaveChangesAsync();

        _arrClient.GetCommandStatusAsync(Arg.Any<ArrInstance>(), Arg.Any<long>())
            .Returns(new ArrCommandStatus(1, ArrCommandState.Completed, null));

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

        _eventsContext.SeekerCommandTrackers.Add(new SeekerCommandTracker
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
        await _eventsContext.SaveChangesAsync();

        _arrClient.GetCommandStatusAsync(Arg.Any<ArrInstance>(), Arg.Any<long>())
            .Returns(new ArrCommandStatus(1, ArrCommandState.Completed, null));

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

    [Fact]
    public async Task Publishes_timed_out_status_when_command_exceeds_timeout()
    {
        // Arrange
        ArrInstance radarrInstance = TestDataContextFactory.AddRadarrInstance(_dataContext);
        Guid eventId = Guid.NewGuid();

        _eventsContext.SeekerCommandTrackers.Add(new SeekerCommandTracker
        {
            ArrInstanceId = radarrInstance.Id,
            CommandId = 7,
            EventId = eventId,
            ExternalItemId = 300,
            ItemTitle = "Stuck Movie",
            SeasonNumber = 0,
            Status = SearchCommandStatus.Pending,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime - TimeSpan.FromMinutes(11)
        });
        await _dataContext.SaveChangesAsync();
        await _eventsContext.SaveChangesAsync();

        _arrClient.GetCommandStatusAsync(Arg.Any<ArrInstance>(), Arg.Any<long>())
            .Returns(new ArrCommandStatus(7, ArrCommandState.Started, null));

        Task<SearchCommandStatus> publishTask = CaptureNextPublishedStatus();

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        SearchCommandStatus publishedStatus = await publishTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        publishedStatus.ShouldBe(SearchCommandStatus.TimedOut);

        await _eventPublisher.Received(1).PublishSearchCompleted(
            eventId, SearchCommandStatus.TimedOut, Arg.Any<InstanceType>(), Arg.Any<string>(), Arg.Any<List<string>?>());

        await _arrClient.DidNotReceive().GetCommandStatusAsync(Arg.Any<ArrInstance>(), Arg.Any<long>());
        await _arrClient.DidNotReceive().GetQueueItemsAsync(Arg.Any<ArrInstance>(), Arg.Any<int>());
    }

    [Fact]
    public async Task Publishes_failed_status_when_the_command_reports_failed()
    {
        // Arrange
        ArrInstance radarrInstance = TestDataContextFactory.AddRadarrInstance(_dataContext);
        Guid eventId = Guid.NewGuid();
        AddTracker(radarrInstance.Id, eventId);
        await _dataContext.SaveChangesAsync();
        await _eventsContext.SaveChangesAsync();

        _arrClient.GetCommandStatusAsync(Arg.Any<ArrInstance>(), Arg.Any<long>())
            .Returns(new ArrCommandStatus(1, ArrCommandState.Failed, null));

        Task<SearchCommandStatus> publishTask = CaptureNextPublishedStatus();

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        SearchCommandStatus publishedStatus = await publishTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        publishedStatus.ShouldBe(SearchCommandStatus.Failed);
        await _arrClient.DidNotReceive().GetQueueItemsAsync(Arg.Any<ArrInstance>(), Arg.Any<int>());
    }

    [Theory]
    [InlineData(ArrCommandState.Aborted)]
    [InlineData(ArrCommandState.Cancelled)]
    [InlineData(ArrCommandState.Orphaned)]
    public async Task Publishes_failed_status_for_every_unsuccessful_arr_command_state(ArrCommandState state)
    {
        // Arrange
        ArrInstance radarrInstance = TestDataContextFactory.AddRadarrInstance(_dataContext);
        Guid eventId = Guid.NewGuid();
        AddTracker(radarrInstance.Id, eventId);
        await _dataContext.SaveChangesAsync();
        await _eventsContext.SaveChangesAsync();

        _arrClient.GetCommandStatusAsync(Arg.Any<ArrInstance>(), Arg.Any<long>())
            .Returns(new ArrCommandStatus(1, state, null));

        Task<SearchCommandStatus> publishTask = CaptureNextPublishedStatus();

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        SearchCommandStatus publishedStatus = await publishTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        publishedStatus.ShouldBe(SearchCommandStatus.Failed);
    }

    [Fact]
    public async Task Publishes_completed_status_when_arr_no_longer_knows_the_command()
    {
        // Arrange
        ArrInstance radarrInstance = TestDataContextFactory.AddRadarrInstance(_dataContext);
        Guid eventId = Guid.NewGuid();
        AddTracker(radarrInstance.Id, eventId);
        await _dataContext.SaveChangesAsync();
        await _eventsContext.SaveChangesAsync();

        _arrClient.GetCommandStatusAsync(Arg.Any<ArrInstance>(), Arg.Any<long>())
            .ThrowsAsync(new HttpRequestException("Not found", null, HttpStatusCode.NotFound));
        _arrClient.GetQueueItemsAsync(Arg.Any<ArrInstance>(), Arg.Any<int>())
            .Returns(new QueueListResponse());

        Task<SearchCommandStatus> publishTask = CaptureNextPublishedStatus();

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        SearchCommandStatus publishedStatus = await publishTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        publishedStatus.ShouldBe(SearchCommandStatus.Completed);
    }

    [Fact]
    public async Task Keeps_polling_when_the_arr_command_state_is_not_recognized()
    {
        // Arrange
        ArrInstance radarrInstance = TestDataContextFactory.AddRadarrInstance(_dataContext);
        Guid eventId = Guid.NewGuid();
        AddTracker(radarrInstance.Id, eventId);
        await _dataContext.SaveChangesAsync();
        await _eventsContext.SaveChangesAsync();

        _arrClient.GetCommandStatusAsync(Arg.Any<ArrInstance>(), Arg.Any<long>())
            .Returns(new ArrCommandStatus(1, ArrCommandState.Unknown, null));

        Task secondPoll = CaptureNthPoll(2);
        CaptureNextPublishedStatus();

        // Act
        await _sut.StartAsync(_cts.Token);
        await AdvanceUntilAsync(secondPoll);

        // Assert
        await _eventPublisher.DidNotReceive().PublishSearchCompleted(
            Arg.Any<Guid>(), Arg.Any<SearchCommandStatus>(), Arg.Any<InstanceType>(), Arg.Any<string>(), Arg.Any<List<string>?>());

        SeekerCommandTracker tracker = _eventsContext.SeekerCommandTrackers.AsNoTracking().Single();
        tracker.Status.ShouldBe(SearchCommandStatus.Pending);
    }

    [Fact]
    public async Task Removes_the_tracker_after_publishing_the_outcome()
    {
        // Arrange
        ArrInstance radarrInstance = TestDataContextFactory.AddRadarrInstance(_dataContext);
        AddTracker(radarrInstance.Id, Guid.NewGuid());
        await _dataContext.SaveChangesAsync();
        await _eventsContext.SaveChangesAsync();

        _arrClient.GetCommandStatusAsync(Arg.Any<ArrInstance>(), Arg.Any<long>())
            .Returns(new ArrCommandStatus(1, ArrCommandState.Completed, null));
        _arrClient.GetQueueItemsAsync(Arg.Any<ArrInstance>(), Arg.Any<int>())
            .Returns(new QueueListResponse());

        Task<SearchCommandStatus> publishTask = CaptureNextPublishedStatus();

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        await publishTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        await WaitForTrackerCountAsync(0);
    }

    [Fact]
    public async Task Keeps_the_tracker_when_publishing_the_outcome_fails()
    {
        // Arrange
        ArrInstance radarrInstance = TestDataContextFactory.AddRadarrInstance(_dataContext);
        AddTracker(radarrInstance.Id, Guid.NewGuid());
        await _dataContext.SaveChangesAsync();
        await _eventsContext.SaveChangesAsync();

        _arrClient.GetCommandStatusAsync(Arg.Any<ArrInstance>(), Arg.Any<long>())
            .Returns(new ArrCommandStatus(1, ArrCommandState.Completed, null));
        _arrClient.GetQueueItemsAsync(Arg.Any<ArrInstance>(), Arg.Any<int>())
            .Returns(new QueueListResponse());

        Task publishAttempt = FailNextPublish();

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        await publishAttempt.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        await WaitForTrackerCountAsync(1);
    }

    [Fact]
    public async Task Publishes_a_terminal_tracker_left_over_from_a_previous_cycle()
    {
        // Arrange
        ArrInstance radarrInstance = TestDataContextFactory.AddRadarrInstance(_dataContext);
        Guid eventId = Guid.NewGuid();
        AddTracker(radarrInstance.Id, eventId, status: SearchCommandStatus.Completed);
        await _dataContext.SaveChangesAsync();
        await _eventsContext.SaveChangesAsync();

        _arrClient.GetQueueItemsAsync(Arg.Any<ArrInstance>(), Arg.Any<int>())
            .Returns(new QueueListResponse());

        Task<SearchCommandStatus> publishTask = CaptureNextPublishedStatus();

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        SearchCommandStatus publishedStatus = await publishTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        publishedStatus.ShouldBe(SearchCommandStatus.Completed);
        await _arrClient.DidNotReceive().GetCommandStatusAsync(Arg.Any<ArrInstance>(), Arg.Any<long>());
    }

    [Fact]
    public async Task Abandons_a_tracker_that_keeps_failing_to_publish()
    {
        // Arrange
        ArrInstance radarrInstance = TestDataContextFactory.AddRadarrInstance(_dataContext);
        AddTracker(radarrInstance.Id, Guid.NewGuid(), status: SearchCommandStatus.Completed, age: TimeSpan.FromMinutes(31));
        await _dataContext.SaveChangesAsync();
        await _eventsContext.SaveChangesAsync();

        _arrClient.GetQueueItemsAsync(Arg.Any<ArrInstance>(), Arg.Any<int>())
            .Returns(new QueueListResponse());

        Task publishAttempt = FailNextPublish();

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        await publishAttempt.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        await WaitForTrackerCountAsync(0);
    }

    [Fact]
    public async Task Fails_the_event_when_the_arr_instance_no_longer_exists()
    {
        // Arrange
        Guid eventId = Guid.NewGuid();
        AddTracker(Guid.NewGuid(), eventId, status: SearchCommandStatus.Completed);
        await _eventsContext.SaveChangesAsync();

        Task<SearchCommandStatus> publishTask = CaptureNextPublishedStatus();

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        SearchCommandStatus publishedStatus = await publishTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        publishedStatus.ShouldBe(SearchCommandStatus.Failed);

        await _eventPublisher.Received(1).PublishSearchCompleted(
            eventId, SearchCommandStatus.Failed, Arg.Any<InstanceType>(), Arg.Any<string>(), Arg.Any<List<string>?>());

        await WaitForTrackerCountAsync(0);
    }

    [Fact]
    public async Task Fails_a_pending_tracker_whose_arr_instance_no_longer_exists()
    {
        // Arrange
        Guid eventId = Guid.NewGuid();
        AddTracker(Guid.NewGuid(), eventId);
        await _eventsContext.SaveChangesAsync();

        Task<SearchCommandStatus> publishTask = CaptureNextPublishedStatus();

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        SearchCommandStatus publishedStatus = await publishTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        publishedStatus.ShouldBe(SearchCommandStatus.Failed);

        await _eventPublisher.Received(1).PublishSearchCompleted(
            eventId, SearchCommandStatus.Failed, Arg.Any<InstanceType>(), Arg.Any<string>(), Arg.Any<List<string>?>());

        await _arrClient.DidNotReceive().GetCommandStatusAsync(Arg.Any<ArrInstance>(), Arg.Any<long>());

        await WaitForTrackerCountAsync(0);
    }

    private void AddTracker(
        Guid arrInstanceId,
        Guid eventId,
        long commandId = 1,
        TimeSpan? age = null,
        SearchCommandStatus status = SearchCommandStatus.Pending)
    {
        _eventsContext.SeekerCommandTrackers.Add(new SeekerCommandTracker
        {
            ArrInstanceId = arrInstanceId,
            CommandId = commandId,
            EventId = eventId,
            ExternalItemId = 300,
            ItemTitle = "Test Item",
            SeasonNumber = 0,
            Status = status,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime - (age ?? TimeSpan.Zero)
        });
    }

    private Task FailNextPublish()
    {
        TaskCompletionSource tcs = new();

        _eventPublisher.PublishSearchCompleted(
                Arg.Any<Guid>(), Arg.Any<SearchCommandStatus>(), Arg.Any<InstanceType>(), Arg.Any<string>(), Arg.Any<List<string>?>())
            .Returns<Task>(_ =>
            {
                tcs.TrySetResult();
                throw new InvalidOperationException("publish failed");
            });

        return tcs.Task;
    }

    private async Task WaitForTrackerCountAsync(int expected)
    {
        for (int attempt = 0; attempt < 200; attempt++)
        {
            try
            {
                if (await _eventsContext.SeekerCommandTrackers.CountAsync() == expected)
                {
                    return;
                }
            }
            catch (InvalidOperationException)
            {
                // the monitor is mid-cycle on the shared context
            }

            await Task.Delay(10);
        }

        (await _eventsContext.SeekerCommandTrackers.CountAsync()).ShouldBe(expected);
    }

    private Task<SearchCommandStatus> CaptureNextPublishedStatus()
    {
        TaskCompletionSource<SearchCommandStatus> tcs = new();

        _eventPublisher.PublishSearchCompleted(
                Arg.Any<Guid>(), Arg.Any<SearchCommandStatus>(), Arg.Any<InstanceType>(), Arg.Any<string>(), Arg.Any<List<string>?>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => tcs.TrySetResult(ci.ArgAt<SearchCommandStatus>(1)));

        return tcs.Task;
    }

    private Task CaptureNthPoll(int count)
    {
        TaskCompletionSource tcs = new();
        int seen = 0;

        _arrClient
            .When(client => client.GetCommandStatusAsync(Arg.Any<ArrInstance>(), Arg.Any<long>()))
            .Do(_ =>
            {
                if (Interlocked.Increment(ref seen) >= count)
                {
                    tcs.TrySetResult();
                }
            });

        return tcs.Task;
    }

    private async Task AdvanceUntilAsync(Task signal)
    {
        _timeProvider.Advance(TimeSpan.FromSeconds(11));

        for (int attempt = 0; attempt < 100 && !signal.IsCompleted; attempt++)
        {
            await Task.Delay(10);
            _timeProvider.Advance(TimeSpan.FromSeconds(15));
        }

        await signal.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
