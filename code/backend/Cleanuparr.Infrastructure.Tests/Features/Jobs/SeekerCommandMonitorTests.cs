using System.Net;
using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events.Interfaces;
using Cleanuparr.Infrastructure.Features.Arr;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.Jobs;
using Cleanuparr.Infrastructure.Tests.Features.Jobs.TestHelpers;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Events;
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
    private readonly ILogger<SeekerCommandMonitor> _logger;
    private readonly SeekerCommandMonitor _sut;
    private readonly CancellationTokenSource _cts;

    public SeekerCommandMonitorTests()
    {
        _dataContext = TestDataContextFactory.Create();
        _eventsContext = TestDataContextFactory.CreateEvents();
        _timeProvider = new FakeTimeProvider();
        _arrClient = Substitute.For<IArrClient>();
        _eventPublisher = Substitute.For<IEventPublisher>();
        _logger = Substitute.For<ILogger<SeekerCommandMonitor>>();
        _cts = new CancellationTokenSource();

        var arrClientFactory = Substitute.For<IArrClientFactory>();

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(DataContext)).Returns(_dataContext);
        serviceProvider.GetService(typeof(EventsContext)).Returns(_eventsContext);
        serviceProvider.GetService(typeof(IArrClientFactory)).Returns(arrClientFactory);
        serviceProvider.GetService(typeof(IArrQueueReader))
            .Returns(new ArrQueueReader(Substitute.For<ILogger<ArrQueueReader>>()));
        serviceProvider.GetService(typeof(IEventPublisher)).Returns(_eventPublisher);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        arrClientFactory.GetClient(Arg.Any<InstanceType>(), Arg.Any<float>()).Returns(_arrClient);

        _sut = new SeekerCommandMonitor(_logger, scopeFactory, _timeProvider);
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

        StubCommandState(ArrCommandState.Completed);

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

        StubCommandState(ArrCommandState.Completed);

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

        StubCommandState(ArrCommandState.Completed);

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
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime - TimeSpan.FromMinutes(31)
        });
        await _dataContext.SaveChangesAsync();
        await _eventsContext.SaveChangesAsync();

        StubCommandState(ArrCommandState.Started, commandId: 7);

        Task<SearchCommandStatus> publishTask = CaptureNextPublishedStatus();

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        SearchCommandStatus publishedStatus = await publishTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        publishedStatus.ShouldBe(SearchCommandStatus.TimedOut);

        await _eventPublisher.Received(1).PublishSearchCompleted(
            eventId, SearchCommandStatus.TimedOut, Arg.Any<InstanceType>(), Arg.Any<string>(), Arg.Any<List<string>?>());

        await _arrClient.Received(1).GetCommandsAsync(Arg.Any<ArrInstance>());
        await _arrClient.DidNotReceive().GetCommandStatusAsync(Arg.Any<ArrInstance>(), Arg.Any<long>());
        await _arrClient.DidNotReceive().GetQueueItemsAsync(Arg.Any<ArrInstance>(), Arg.Any<int>());
    }

    [Fact]
    public async Task Completes_a_command_that_finished_just_after_the_timeout()
    {
        // Arrange
        ArrInstance radarrInstance = TestDataContextFactory.AddRadarrInstance(_dataContext);
        Guid eventId = Guid.NewGuid();
        AddTracker(radarrInstance.Id, eventId, commandId: 7, age: TimeSpan.FromMinutes(31));
        await _dataContext.SaveChangesAsync();
        await _eventsContext.SaveChangesAsync();

        StubCommandState(ArrCommandState.Completed, commandId: 7);
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
    public async Task Does_not_time_out_a_command_that_is_still_within_the_timeout()
    {
        // Arrange
        ArrInstance radarrInstance = TestDataContextFactory.AddRadarrInstance(_dataContext);
        AddTracker(radarrInstance.Id, Guid.NewGuid(), age: TimeSpan.FromMinutes(10));
        await _dataContext.SaveChangesAsync();
        await _eventsContext.SaveChangesAsync();

        StubCommandState(ArrCommandState.Started);

        Task secondPoll = CaptureNthPoll(2);
        CaptureNextPublishedStatus();

        // Act
        await _sut.StartAsync(_cts.Token);
        await AdvanceUntilAsync(secondPoll);

        // Assert
        await _eventPublisher.DidNotReceive().PublishSearchCompleted(
            Arg.Any<Guid>(), Arg.Any<SearchCommandStatus>(), Arg.Any<InstanceType>(), Arg.Any<string>(), Arg.Any<List<string>?>());

        SeekerCommandTracker tracker = _eventsContext.SeekerCommandTrackers.AsNoTracking().Single();
        tracker.Status.ShouldBe(SearchCommandStatus.Started);
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

        StubCommandState(ArrCommandState.Failed);

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

        StubCommandState(state);

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

        _arrClient.GetCommandsAsync(Arg.Any<ArrInstance>())
            .ThrowsAsync(new HttpRequestException("Not found", null, HttpStatusCode.NotFound));
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

        StubCommandState(ArrCommandState.Unknown);

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

        StubCommandState(ArrCommandState.Completed);
        _arrClient.GetQueueItemsAsync(Arg.Any<ArrInstance>(), Arg.Any<int>())
            .Returns(new QueueListResponse());

        Task<SearchCommandStatus> publishTask = CaptureNextPublishedStatus(out Task savedAfterPublish);

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        await publishTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        await savedAfterPublish.WaitAsync(TimeSpan.FromSeconds(5));
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

        StubCommandState(ArrCommandState.Completed);
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
        AddTracker(radarrInstance.Id, Guid.NewGuid(), status: SearchCommandStatus.Completed, age: TimeSpan.FromMinutes(91));
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

        Task<SearchCommandStatus> publishTask = CaptureNextPublishedStatus(out Task savedAfterPublish);

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        SearchCommandStatus publishedStatus = await publishTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        publishedStatus.ShouldBe(SearchCommandStatus.Failed);

        await _eventPublisher.Received(1).PublishSearchCompleted(
            eventId, SearchCommandStatus.Failed, Arg.Any<InstanceType>(), Arg.Any<string>(), Arg.Any<List<string>?>());

        await savedAfterPublish.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForTrackerCountAsync(0);
    }

    [Fact]
    public async Task Fails_a_pending_tracker_whose_arr_instance_no_longer_exists()
    {
        // Arrange
        Guid eventId = Guid.NewGuid();
        AddTracker(Guid.NewGuid(), eventId);
        await _eventsContext.SaveChangesAsync();

        Task<SearchCommandStatus> pendingPublishTask = CaptureNextPublishedStatus(out Task savedAfterPublish);

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        SearchCommandStatus pendingPublishedStatus = await pendingPublishTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        pendingPublishedStatus.ShouldBe(SearchCommandStatus.Failed);

        await _eventPublisher.Received(1).PublishSearchCompleted(
            eventId, SearchCommandStatus.Failed, Arg.Any<InstanceType>(), Arg.Any<string>(), Arg.Any<List<string>?>());

        await _arrClient.DidNotReceive().GetCommandsAsync(Arg.Any<ArrInstance>());

        await savedAfterPublish.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForTrackerCountAsync(0);
    }

    [Fact]
    public async Task Polls_every_command_of_an_instance_in_a_single_request()
    {
        // Arrange
        ArrInstance radarrInstance = TestDataContextFactory.AddRadarrInstance(_dataContext);
        AddTracker(radarrInstance.Id, Guid.NewGuid(), commandId: 1);
        AddTracker(radarrInstance.Id, Guid.NewGuid(), commandId: 2);
        AddTracker(radarrInstance.Id, Guid.NewGuid(), commandId: 3);
        await _dataContext.SaveChangesAsync();
        await _eventsContext.SaveChangesAsync();

        _arrClient.GetCommandsAsync(Arg.Any<ArrInstance>())
            .Returns(
            [
                new ArrCommandStatus(1, ArrCommandState.Completed, null),
                new ArrCommandStatus(2, ArrCommandState.Completed, null),
                new ArrCommandStatus(3, ArrCommandState.Completed, null),
            ]);
        _arrClient.GetQueueItemsAsync(Arg.Any<ArrInstance>(), Arg.Any<int>())
            .Returns(new QueueListResponse());

        CaptureNextPublishedStatus(out Task savedAfterPublish);

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        await savedAfterPublish.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForTrackerCountAsync(0);

        // Assert
        await _arrClient.Received(1).GetCommandsAsync(Arg.Any<ArrInstance>());
        await _arrClient.DidNotReceive().GetCommandStatusAsync(Arg.Any<ArrInstance>(), Arg.Any<long>());
        await _arrClient.Received(1).GetQueueItemsAsync(Arg.Any<ArrInstance>(), Arg.Any<int>());
    }

    [Fact]
    public async Task Completes_a_command_that_is_missing_from_the_command_list()
    {
        // Arrange
        ArrInstance radarrInstance = TestDataContextFactory.AddRadarrInstance(_dataContext);
        AddTracker(radarrInstance.Id, Guid.NewGuid(), commandId: 42);
        await _dataContext.SaveChangesAsync();
        await _eventsContext.SaveChangesAsync();

        _arrClient.GetCommandsAsync(Arg.Any<ArrInstance>())
            .Returns([new ArrCommandStatus(7, ArrCommandState.Started, null)]);
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
    public async Task Falls_back_to_individual_command_checks_when_the_command_list_fails()
    {
        // Arrange
        ArrInstance radarrInstance = TestDataContextFactory.AddRadarrInstance(_dataContext);
        AddTracker(radarrInstance.Id, Guid.NewGuid());
        await _dataContext.SaveChangesAsync();
        await _eventsContext.SaveChangesAsync();

        _arrClient.GetCommandsAsync(Arg.Any<ArrInstance>())
            .ThrowsAsync(new HttpRequestException("boom"));
        _arrClient.GetCommandStatusAsync(Arg.Any<ArrInstance>(), Arg.Any<long>())
            .Returns(new ArrCommandStatus(1, ArrCommandState.Failed, null));

        Task<SearchCommandStatus> publishTask = CaptureNextPublishedStatus();

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        SearchCommandStatus publishedStatus = await publishTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        publishedStatus.ShouldBe(SearchCommandStatus.Failed);
        await _arrClient.Received(1).GetCommandStatusAsync(Arg.Any<ArrInstance>(), Arg.Any<long>());
    }

    [Fact]
    public async Task Reports_grabbed_items_found_beyond_the_first_queue_page()
    {
        // Arrange
        ArrInstance radarrInstance = TestDataContextFactory.AddRadarrInstance(_dataContext);
        AddTracker(radarrInstance.Id, Guid.NewGuid());
        await _dataContext.SaveChangesAsync();
        await _eventsContext.SaveChangesAsync();

        StubCommandState(ArrCommandState.Completed);

        _arrClient.GetQueueItemsAsync(Arg.Any<ArrInstance>(), 1)
            .Returns(new QueueListResponse
            {
                TotalRecords = 2,
                Records = [new QueueRecord { Id = 1, MovieId = 999, Title = "Other.Movie", DownloadId = "OTHER", Protocol = "torrent", Status = "downloading" }]
            });
        _arrClient.GetQueueItemsAsync(Arg.Any<ArrInstance>(), 2)
            .Returns(new QueueListResponse
            {
                TotalRecords = 2,
                Records = [new QueueRecord { Id = 2, MovieId = 300, Title = "Wanted.Movie", DownloadId = "WANTED", Protocol = "torrent", Status = "downloading" }]
            });

        TaskCompletionSource<List<string>?> grabbedTcs = new();
        _eventPublisher.PublishSearchCompleted(
                Arg.Any<Guid>(), Arg.Any<SearchCommandStatus>(), Arg.Any<InstanceType>(), Arg.Any<string>(), Arg.Any<List<string>?>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => grabbedTcs.TrySetResult(ci.ArgAt<List<string>?>(4)));

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        List<string>? grabbedItems = await grabbedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        grabbedItems.ShouldNotBeNull();
        grabbedItems!.ShouldBe(["Wanted.Movie"]);
    }

    [Fact]
    public async Task Defers_the_outcome_until_the_queue_can_be_read()
    {
        // Arrange
        ArrInstance radarrInstance = TestDataContextFactory.AddRadarrInstance(_dataContext);
        Guid eventId = Guid.NewGuid();
        AddTracker(radarrInstance.Id, eventId);
        await _dataContext.SaveChangesAsync();
        await _eventsContext.SaveChangesAsync();

        StubCommandState(ArrCommandState.Completed);

        int queueReadAttempts = 0;
        TaskCompletionSource firstReadTcs = new();
        _arrClient.GetQueueItemsAsync(Arg.Any<ArrInstance>(), Arg.Any<int>())
            .Returns<Task<QueueListResponse>>(_ =>
            {
                queueReadAttempts++;

                if (queueReadAttempts == 1)
                {
                    firstReadTcs.TrySetResult();
                    throw new HttpRequestException("queue unavailable");
                }

                return Task.FromResult(new QueueListResponse
                {
                    TotalRecords = 1,
                    Records = [new QueueRecord { Id = 1, MovieId = 300, Title = "Test.Item.1080p", DownloadId = "HASH1", Protocol = "torrent", Status = "downloading" }]
                });
            });

        Task<SearchCommandStatus> publishTask = CaptureNextPublishedStatus(out Task savedAfterPublish);

        // Act: the first poll hits a failing queue read
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        await firstReadTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(100);

        // Assert: the outcome is deferred, the tracker stays
        await _eventPublisher.DidNotReceive().PublishSearchCompleted(
            Arg.Any<Guid>(), Arg.Any<SearchCommandStatus>(), Arg.Any<InstanceType>(), Arg.Any<string>(), Arg.Any<List<string>?>());
        await WaitForTrackerCountAsync(1);

        // Act: the second poll reads the queue successfully
        _timeProvider.Advance(TimeSpan.FromSeconds(60));
        SearchCommandStatus publishedStatus = await publishTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert: the deferred outcome publishes with the grabbed item and the tracker is removed
        publishedStatus.ShouldBe(SearchCommandStatus.Completed);
        await _eventPublisher.Received(1).PublishSearchCompleted(
            eventId, SearchCommandStatus.Completed, Arg.Any<InstanceType>(), Arg.Any<string>(),
            Arg.Is<List<string>?>(items => items != null && items.Count == 1 && items[0] == "Test.Item.1080p"));
        await savedAfterPublish.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForTrackerCountAsync(0);
    }

    [Fact]
    public async Task Keeps_polling_when_an_individual_command_check_fails()
    {
        // Arrange
        ArrInstance radarrInstance = TestDataContextFactory.AddRadarrInstance(_dataContext);
        AddTracker(radarrInstance.Id, Guid.NewGuid());
        await _dataContext.SaveChangesAsync();
        await _eventsContext.SaveChangesAsync();

        _arrClient.GetCommandsAsync(Arg.Any<ArrInstance>())
            .ThrowsAsync(new HttpRequestException("command list unavailable"));
        _arrClient.GetCommandStatusAsync(Arg.Any<ArrInstance>(), Arg.Any<long>())
            .ThrowsAsync(new HttpRequestException("command status unavailable"));

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
    public async Task Propagates_the_started_status_to_the_search_event()
    {
        // Arrange
        ArrInstance radarrInstance = TestDataContextFactory.AddRadarrInstance(_dataContext);
        Guid eventId = Guid.NewGuid();
        AddTracker(radarrInstance.Id, eventId);
        await _dataContext.SaveChangesAsync();
        await _eventsContext.SaveChangesAsync();

        StubCommandState(ArrCommandState.Started);

        TaskCompletionSource progressTcs = new();
        _eventPublisher.PublishSearchStarted(Arg.Any<Guid>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => progressTcs.TrySetResult());

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        await progressTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        await _eventPublisher.Received(1).PublishSearchStarted(eventId);

        await _eventPublisher.DidNotReceive().PublishSearchCompleted(
            Arg.Any<Guid>(), Arg.Any<SearchCommandStatus>(), Arg.Any<InstanceType>(), Arg.Any<string>(), Arg.Any<List<string>?>());
    }

    [Fact]
    public async Task Sweeps_search_events_that_never_got_a_tracker()
    {
        // Arrange
        TaskCompletionSource sweepTcs = new();
        _eventPublisher.FailAbandonedSearchEvents(Arg.Any<DateTimeOffset>())
            .Returns(1)
            .AndDoes(_ => sweepTcs.TrySetResult());

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        await sweepTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        await _eventPublisher.Received(1).FailAbandonedSearchEvents(
            _timeProvider.GetUtcNow() - TimeSpan.FromMinutes(60));
    }

    [Fact]
    public async Task Reports_the_grab_of_a_sonarr_episode_search()
    {
        // Arrange
        ArrInstance sonarrInstance = TestDataContextFactory.AddSonarrInstance(_dataContext);

        // Act
        List<string>? grabbedItems = await CaptureGrabbedItemsAsync(
            Tracker(sonarrInstance.Id, externalItemId: 42, episodeId: 9001, itemTitle: "Example Show - S03E04"),
            new QueueRecord { Id = 1, SeriesId = 42, EpisodeId = 9001, SeasonNumber = 3, Title = "Example.Show.S03E04.1080p", DownloadId = "EP9001", Protocol = "torrent", Status = "downloading" });

        // Assert
        grabbedItems.ShouldNotBeNull();
        grabbedItems!.ShouldBe(["Example.Show.S03E04.1080p"]);
    }

    [Fact]
    public async Task Ignores_another_episode_of_the_same_series()
    {
        // Arrange
        ArrInstance sonarrInstance = TestDataContextFactory.AddSonarrInstance(_dataContext);

        // Act
        List<string>? grabbedItems = await CaptureGrabbedItemsAsync(
            Tracker(sonarrInstance.Id, externalItemId: 42, episodeId: 9001, itemTitle: "Example Show - S03E04"),
            new QueueRecord { Id = 1, SeriesId = 42, EpisodeId = 9001, SeasonNumber = 3, Title = "Example.Show.S03E04.1080p", DownloadId = "EP9001", Protocol = "torrent", Status = "downloading" },
            new QueueRecord { Id = 2, SeriesId = 42, EpisodeId = 9002, SeasonNumber = 3, Title = "Example.Show.S03E05.1080p", DownloadId = "EP9002", Protocol = "torrent", Status = "downloading" });

        // Assert
        grabbedItems.ShouldNotBeNull();
        grabbedItems!.ShouldBe(["Example.Show.S03E04.1080p"]);
    }

    [Fact]
    public async Task Ignores_another_season_of_a_specials_search()
    {
        // Arrange
        ArrInstance sonarrInstance = TestDataContextFactory.AddSonarrInstance(_dataContext);

        // Act
        List<string>? grabbedItems = await CaptureGrabbedItemsAsync(
            Tracker(sonarrInstance.Id, externalItemId: 42, seasonNumber: 0, itemTitle: "Example Show S00"),
            new QueueRecord { Id = 1, SeriesId = 42, EpisodeId = 9001, SeasonNumber = 3, Title = "Example.Show.S03E04.1080p", DownloadId = "EP9001", Protocol = "torrent", Status = "downloading" });

        // Assert
        grabbedItems.ShouldBeNull();
    }

    [Fact]
    public async Task Reports_the_grab_of_a_whisparr_v3_search()
    {
        // Arrange
        ArrInstance whisparrInstance = TestDataContextFactory.AddWhisparrInstance(_dataContext, version: 3);

        // Act
        List<string>? grabbedItems = await CaptureGrabbedItemsAsync(
            Tracker(whisparrInstance.Id, externalItemId: 55, itemTitle: "Example Scene"),
            new QueueRecord { Id = 1, MovieId = 55, Title = "Example.Scene.1080p", DownloadId = "MV55", Protocol = "torrent", Status = "downloading" });

        // Assert
        grabbedItems.ShouldNotBeNull();
        grabbedItems!.ShouldBe(["Example.Scene.1080p"]);
    }

    [Fact]
    public async Task Reports_the_grab_of_a_lidarr_search()
    {
        // Arrange
        ArrInstance lidarrInstance = TestDataContextFactory.AddLidarrInstance(_dataContext);

        // Act
        List<string>? grabbedItems = await CaptureGrabbedItemsAsync(
            Tracker(lidarrInstance.Id, externalItemId: 77, itemTitle: "Example Album"),
            new QueueRecord { Id = 1, AlbumId = 77, Title = "Example.Album.FLAC", DownloadId = "AL77", Protocol = "torrent", Status = "downloading" });

        // Assert
        grabbedItems.ShouldNotBeNull();
        grabbedItems!.ShouldBe(["Example.Album.FLAC"]);
    }

    [Fact]
    public async Task Reports_the_grab_of_a_proactive_lidarr_album_search()
    {
        // Arrange
        ArrInstance lidarrInstance = TestDataContextFactory.AddLidarrInstance(_dataContext);

        // Act
        List<string>? grabbedItems = await CaptureGrabbedItemsAsync(
            Tracker(lidarrInstance.Id, externalItemId: 5, episodeId: 77, itemTitle: "Example Artist - Example Album"),
            new QueueRecord { Id = 1, ArtistId = 5, AlbumId = 77, Title = "Example.Album.FLAC", DownloadId = "AL77", Protocol = "torrent", Status = "downloading" });

        // Assert
        grabbedItems.ShouldNotBeNull();
        grabbedItems!.ShouldBe(["Example.Album.FLAC"]);
    }

    [Fact]
    public async Task Ignores_another_album_of_a_proactive_lidarr_search()
    {
        // Arrange
        ArrInstance lidarrInstance = TestDataContextFactory.AddLidarrInstance(_dataContext);

        // Act
        List<string>? grabbedItems = await CaptureGrabbedItemsAsync(
            Tracker(lidarrInstance.Id, externalItemId: 5, episodeId: 77, itemTitle: "Example Artist - Example Album"),
            new QueueRecord { Id = 1, ArtistId = 5, AlbumId = 78, Title = "Example.Other.FLAC", DownloadId = "AL78", Protocol = "torrent", Status = "downloading" });

        // Assert
        grabbedItems.ShouldBeNull();
    }

    [Fact]
    public async Task Reports_the_grab_of_a_readarr_search()
    {
        // Arrange
        ArrInstance readarrInstance = TestDataContextFactory.AddReadarrInstance(_dataContext);

        // Act
        List<string>? grabbedItems = await CaptureGrabbedItemsAsync(
            Tracker(readarrInstance.Id, externalItemId: 88, itemTitle: "Example Book"),
            new QueueRecord { Id = 1, BookId = 88, Title = "Example.Book.EPUB", DownloadId = "BK88", Protocol = "torrent", Status = "downloading" });

        // Assert
        grabbedItems.ShouldNotBeNull();
        grabbedItems!.ShouldBe(["Example.Book.EPUB"]);
    }

    #region RecordUnitOutcomeAsync

    [Fact]
    public async Task Grabbed_outcome_marks_the_unit_grabbed_and_increments_counters()
    {
        // Arrange
        ArrInstance radarrInstance = TestDataContextFactory.AddRadarrInstance(_dataContext);
        Guid cycleId = Guid.NewGuid();
        Guid eventId = SetUpUnitOutcomeScenario(radarrInstance.Id, cycleId, externalItemId: 500);

        AddTracker(radarrInstance.Id, eventId, externalItemId: 500);
        await _dataContext.SaveChangesAsync();
        await _eventsContext.SaveChangesAsync();

        StubCommandState(ArrCommandState.Completed);
        _arrClient.GetQueueItemsAsync(Arg.Any<ArrInstance>(), Arg.Any<int>())
            .Returns(new QueueListResponse
            {
                TotalRecords = 1,
                Records = [new QueueRecord { Id = 1, MovieId = 500, Title = "Grabbed.Movie", DownloadId = "HASH500", Protocol = "torrent", Status = "downloading" }]
            });

        Task<SearchCommandStatus> publishTask = CaptureNextPublishedStatus();

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        await publishTask.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForTrackerCountAsync(0);

        // Assert
        SeekerHistory unit = await _eventsContext.SeekerHistory.AsNoTracking().SingleAsync(h => h.ArrInstanceId == radarrInstance.Id);
        unit.LastOutcome.ShouldBe(SeekerSearchOutcome.Grabbed);
        unit.Grabs.ShouldBe(1);

        SeekerCycle cycle = await _eventsContext.SeekerCycles.AsNoTracking().SingleAsync(c => c.Id == cycleId);
        cycle.GrabbedUnits.ShouldBe(1);
    }

    [Fact]
    public async Task Grabbed_outcome_of_an_episode_search_marks_only_the_episode_unit()
    {
        // Arrange
        ArrInstance sonarrInstance = TestDataContextFactory.AddSonarrInstance(_dataContext);
        Guid cycleId = Guid.NewGuid();
        Guid eventId = Guid.NewGuid();

        _eventsContext.Events.Add(new AppEvent
        {
            Id = eventId,
            EventType = EventType.SearchTriggered,
            Message = "Search triggered",
            Severity = EventSeverity.Information,
            CycleId = cycleId,
        });
        _eventsContext.SeekerCycles.Add(new SeekerCycle { Id = cycleId, ArrInstanceId = sonarrInstance.Id });
        _eventsContext.SeekerHistory.Add(new SeekerHistory
        {
            ArrInstanceId = sonarrInstance.Id,
            ExternalItemId = 42,
            ItemType = InstanceType.Sonarr,
            SeasonNumber = 0,
            CycleId = cycleId,
            ItemTitle = "Example Show S00",
        });
        _eventsContext.SeekerHistory.Add(new SeekerHistory
        {
            ArrInstanceId = sonarrInstance.Id,
            ExternalItemId = 42,
            ItemType = InstanceType.Sonarr,
            SeasonNumber = 0,
            EpisodeId = 9001,
            ContentKind = SeekerContentKind.Special,
            CycleId = cycleId,
            ItemTitle = "Example Show S00E04",
        });
        _eventsContext.SeekerCommandTrackers.Add(Tracker(sonarrInstance.Id, externalItemId: 42, episodeId: 9001) with { EventId = eventId });
        await _dataContext.SaveChangesAsync();
        await _eventsContext.SaveChangesAsync();

        StubCommandState(ArrCommandState.Completed);
        _arrClient.GetQueueItemsAsync(Arg.Any<ArrInstance>(), Arg.Any<int>())
            .Returns(new QueueListResponse
            {
                TotalRecords = 1,
                Records = [new QueueRecord { Id = 1, SeriesId = 42, EpisodeId = 9001, SeasonNumber = 0, Title = "Example.Show.S00E04", DownloadId = "EP9001", Protocol = "torrent", Status = "downloading" }]
            });

        Task<SearchCommandStatus> publishTask = CaptureNextPublishedStatus();

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        await publishTask.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForTrackerCountAsync(0);

        // Assert
        List<SeekerHistory> units = await _eventsContext.SeekerHistory.AsNoTracking().ToListAsync();
        units.Single(h => h.EpisodeId == 9001).LastOutcome.ShouldBe(SeekerSearchOutcome.Grabbed);
        units.Single(h => h.EpisodeId == 0).LastOutcome.ShouldBeNull();
    }

    [Fact]
    public async Task NothingGrabbed_outcome_marks_the_unit_and_leaves_counters_untouched()
    {
        // Arrange
        ArrInstance radarrInstance = TestDataContextFactory.AddRadarrInstance(_dataContext);
        Guid cycleId = Guid.NewGuid();
        Guid eventId = SetUpUnitOutcomeScenario(radarrInstance.Id, cycleId, externalItemId: 501);

        AddTracker(radarrInstance.Id, eventId, externalItemId: 501);
        await _dataContext.SaveChangesAsync();
        await _eventsContext.SaveChangesAsync();

        StubCommandState(ArrCommandState.Completed);
        _arrClient.GetQueueItemsAsync(Arg.Any<ArrInstance>(), Arg.Any<int>())
            .Returns(new QueueListResponse());

        Task<SearchCommandStatus> publishTask = CaptureNextPublishedStatus();

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        await publishTask.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForTrackerCountAsync(0);

        // Assert
        SeekerHistory unit = await _eventsContext.SeekerHistory.AsNoTracking().SingleAsync(h => h.ArrInstanceId == radarrInstance.Id);
        unit.LastOutcome.ShouldBe(SeekerSearchOutcome.NothingGrabbed);
        unit.Grabs.ShouldBe(0);

        SeekerCycle cycle = await _eventsContext.SeekerCycles.AsNoTracking().SingleAsync(c => c.Id == cycleId);
        cycle.GrabbedUnits.ShouldBe(0);
    }

    [Fact]
    public async Task Failed_command_marks_the_unit_failed()
    {
        // Arrange
        ArrInstance radarrInstance = TestDataContextFactory.AddRadarrInstance(_dataContext);
        Guid cycleId = Guid.NewGuid();
        Guid eventId = SetUpUnitOutcomeScenario(radarrInstance.Id, cycleId, externalItemId: 502);

        AddTracker(radarrInstance.Id, eventId, externalItemId: 502);
        await _dataContext.SaveChangesAsync();
        await _eventsContext.SaveChangesAsync();

        StubCommandState(ArrCommandState.Failed);

        Task<SearchCommandStatus> publishTask = CaptureNextPublishedStatus();

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        await publishTask.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForTrackerCountAsync(0);

        // Assert
        SeekerHistory unit = await _eventsContext.SeekerHistory.AsNoTracking().SingleAsync(h => h.ArrInstanceId == radarrInstance.Id);
        unit.LastOutcome.ShouldBe(SeekerSearchOutcome.Failed);
    }

    [Fact]
    public async Task Timed_out_command_marks_the_unit_failed()
    {
        // Arrange
        ArrInstance radarrInstance = TestDataContextFactory.AddRadarrInstance(_dataContext);
        Guid cycleId = Guid.NewGuid();
        Guid eventId = SetUpUnitOutcomeScenario(radarrInstance.Id, cycleId, externalItemId: 503);

        AddTracker(radarrInstance.Id, eventId, externalItemId: 503, age: TimeSpan.FromMinutes(31));
        await _dataContext.SaveChangesAsync();
        await _eventsContext.SaveChangesAsync();

        StubCommandState(ArrCommandState.Started);

        Task<SearchCommandStatus> publishTask = CaptureNextPublishedStatus();

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        await publishTask.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForTrackerCountAsync(0);

        // Assert
        SeekerHistory unit = await _eventsContext.SeekerHistory.AsNoTracking().SingleAsync(h => h.ArrInstanceId == radarrInstance.Id);
        unit.LastOutcome.ShouldBe(SeekerSearchOutcome.Failed);
    }

    [Fact]
    public async Task NothingGrabbed_outcome_increments_the_consecutive_misses()
    {
        // Arrange
        ArrInstance radarrInstance = TestDataContextFactory.AddRadarrInstance(_dataContext);
        Guid eventId = SetUpUnitOutcomeScenario(radarrInstance.Id, Guid.NewGuid(), externalItemId: 510, consecutiveMisses: 2);

        AddTracker(radarrInstance.Id, eventId, externalItemId: 510);
        await _dataContext.SaveChangesAsync();
        await _eventsContext.SaveChangesAsync();

        StubCommandState(ArrCommandState.Completed);
        _arrClient.GetQueueItemsAsync(Arg.Any<ArrInstance>(), Arg.Any<int>())
            .Returns(new QueueListResponse());

        Task<SearchCommandStatus> publishTask = CaptureNextPublishedStatus();

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        await publishTask.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForTrackerCountAsync(0);

        // Assert
        SeekerHistory unit = await _eventsContext.SeekerHistory.AsNoTracking().SingleAsync(h => h.ArrInstanceId == radarrInstance.Id);
        unit.ConsecutiveMisses.ShouldBe(3);
    }

    [Fact]
    public async Task Grabbed_outcome_resets_the_consecutive_misses()
    {
        // Arrange
        ArrInstance radarrInstance = TestDataContextFactory.AddRadarrInstance(_dataContext);
        Guid eventId = SetUpUnitOutcomeScenario(radarrInstance.Id, Guid.NewGuid(), externalItemId: 511, consecutiveMisses: 4);

        AddTracker(radarrInstance.Id, eventId, externalItemId: 511);
        await _dataContext.SaveChangesAsync();
        await _eventsContext.SaveChangesAsync();

        StubCommandState(ArrCommandState.Completed);
        _arrClient.GetQueueItemsAsync(Arg.Any<ArrInstance>(), Arg.Any<int>())
            .Returns(new QueueListResponse
            {
                TotalRecords = 1,
                Records = [new QueueRecord { Id = 1, MovieId = 511, Title = "Grabbed.Movie", DownloadId = "HASH511", Protocol = "torrent", Status = "downloading" }]
            });

        Task<SearchCommandStatus> publishTask = CaptureNextPublishedStatus();

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        await publishTask.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForTrackerCountAsync(0);

        // Assert
        SeekerHistory unit = await _eventsContext.SeekerHistory.AsNoTracking().SingleAsync(h => h.ArrInstanceId == radarrInstance.Id);
        unit.ConsecutiveMisses.ShouldBe(0);
    }

    [Fact]
    public async Task NothingGrabbed_outcome_sets_the_last_miss_to_the_search_time()
    {
        // Arrange
        ArrInstance radarrInstance = TestDataContextFactory.AddRadarrInstance(_dataContext);
        DateTimeOffset searchedAt = _timeProvider.GetUtcNow().AddHours(-1);
        Guid eventId = SetUpUnitOutcomeScenario(radarrInstance.Id, Guid.NewGuid(), externalItemId: 516, consecutiveMisses: 2, lastSearchedAt: searchedAt);

        AddTracker(radarrInstance.Id, eventId, externalItemId: 516);
        await _dataContext.SaveChangesAsync();
        await _eventsContext.SaveChangesAsync();

        StubCommandState(ArrCommandState.Completed);
        _arrClient.GetQueueItemsAsync(Arg.Any<ArrInstance>(), Arg.Any<int>())
            .Returns(new QueueListResponse());

        Task<SearchCommandStatus> publishTask = CaptureNextPublishedStatus();

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        await publishTask.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForTrackerCountAsync(0);

        // Assert
        SeekerHistory unit = await _eventsContext.SeekerHistory.AsNoTracking().SingleAsync(h => h.ArrInstanceId == radarrInstance.Id);
        unit.LastMissAt.ShouldBe(searchedAt);
    }

    [Fact]
    public async Task Failed_command_leaves_the_last_miss_unchanged()
    {
        // Arrange
        ArrInstance radarrInstance = TestDataContextFactory.AddRadarrInstance(_dataContext);
        DateTimeOffset searchedAt = _timeProvider.GetUtcNow().AddHours(-1);
        Guid eventId = SetUpUnitOutcomeScenario(radarrInstance.Id, Guid.NewGuid(), externalItemId: 517, consecutiveMisses: 3, lastSearchedAt: searchedAt, lastMissAt: searchedAt.AddDays(-9));

        AddTracker(radarrInstance.Id, eventId, externalItemId: 517);
        await _dataContext.SaveChangesAsync();
        await _eventsContext.SaveChangesAsync();

        StubCommandState(ArrCommandState.Failed);

        Task<SearchCommandStatus> publishTask = CaptureNextPublishedStatus();

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        await publishTask.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForTrackerCountAsync(0);

        // Assert
        SeekerHistory unit = await _eventsContext.SeekerHistory.AsNoTracking().SingleAsync(h => h.ArrInstanceId == radarrInstance.Id);
        unit.LastMissAt.ShouldBe(searchedAt.AddDays(-9));
    }

    [Fact]
    public async Task Grabbed_outcome_clears_the_last_miss()
    {
        // Arrange
        ArrInstance radarrInstance = TestDataContextFactory.AddRadarrInstance(_dataContext);
        DateTimeOffset searchedAt = _timeProvider.GetUtcNow().AddHours(-1);
        Guid eventId = SetUpUnitOutcomeScenario(radarrInstance.Id, Guid.NewGuid(), externalItemId: 515, consecutiveMisses: 4, lastSearchedAt: searchedAt, lastMissAt: searchedAt.AddDays(-9));

        AddTracker(radarrInstance.Id, eventId, externalItemId: 515);
        await _dataContext.SaveChangesAsync();
        await _eventsContext.SaveChangesAsync();

        StubCommandState(ArrCommandState.Completed);
        _arrClient.GetQueueItemsAsync(Arg.Any<ArrInstance>(), Arg.Any<int>())
            .Returns(new QueueListResponse
            {
                TotalRecords = 1,
                Records = [new QueueRecord { Id = 1, MovieId = 515, Title = "Grabbed.Movie", DownloadId = "HASH515", Protocol = "torrent", Status = "downloading" }]
            });

        Task<SearchCommandStatus> publishTask = CaptureNextPublishedStatus();

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        await publishTask.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForTrackerCountAsync(0);

        // Assert
        SeekerHistory unit = await _eventsContext.SeekerHistory.AsNoTracking().SingleAsync(h => h.ArrInstanceId == radarrInstance.Id);
        unit.LastMissAt.ShouldBeNull();
    }

    [Fact]
    public async Task Failed_command_leaves_the_consecutive_misses_unchanged()
    {
        // Arrange
        ArrInstance radarrInstance = TestDataContextFactory.AddRadarrInstance(_dataContext);
        Guid eventId = SetUpUnitOutcomeScenario(radarrInstance.Id, Guid.NewGuid(), externalItemId: 512, consecutiveMisses: 3);

        AddTracker(radarrInstance.Id, eventId, externalItemId: 512);
        await _dataContext.SaveChangesAsync();
        await _eventsContext.SaveChangesAsync();

        StubCommandState(ArrCommandState.Failed);

        Task<SearchCommandStatus> publishTask = CaptureNextPublishedStatus();

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        await publishTask.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForTrackerCountAsync(0);

        // Assert
        SeekerHistory unit = await _eventsContext.SeekerHistory.AsNoTracking().SingleAsync(h => h.ArrInstanceId == radarrInstance.Id);
        unit.LastOutcome.ShouldBe(SeekerSearchOutcome.Failed);
        unit.ConsecutiveMisses.ShouldBe(3);
    }

    [Fact]
    public async Task Timed_out_command_leaves_the_consecutive_misses_unchanged()
    {
        // Arrange
        ArrInstance radarrInstance = TestDataContextFactory.AddRadarrInstance(_dataContext);
        Guid eventId = SetUpUnitOutcomeScenario(radarrInstance.Id, Guid.NewGuid(), externalItemId: 513, consecutiveMisses: 3);

        AddTracker(radarrInstance.Id, eventId, externalItemId: 513, age: TimeSpan.FromMinutes(31));
        await _dataContext.SaveChangesAsync();
        await _eventsContext.SaveChangesAsync();

        StubCommandState(ArrCommandState.Started);

        Task<SearchCommandStatus> publishTask = CaptureNextPublishedStatus();

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        await publishTask.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForTrackerCountAsync(0);

        // Assert
        SeekerHistory unit = await _eventsContext.SeekerHistory.AsNoTracking().SingleAsync(h => h.ArrInstanceId == radarrInstance.Id);
        unit.LastOutcome.ShouldBe(SeekerSearchOutcome.Failed);
        unit.ConsecutiveMisses.ShouldBe(3);
    }

    [Fact]
    public async Task NothingGrabbed_outcome_from_an_older_cycle_leaves_the_consecutive_misses_unchanged()
    {
        // Arrange: the unit was re-searched in a newer cycle before this old search's outcome arrived
        ArrInstance radarrInstance = TestDataContextFactory.AddRadarrInstance(_dataContext);
        Guid oldCycleId = Guid.NewGuid();
        Guid eventId = Guid.NewGuid();

        _eventsContext.Events.Add(new AppEvent
        {
            Id = eventId,
            EventType = EventType.SearchTriggered,
            Message = "Search triggered",
            Severity = EventSeverity.Information,
            CycleId = oldCycleId,
        });
        _eventsContext.SeekerCycles.Add(new SeekerCycle { Id = oldCycleId, ArrInstanceId = radarrInstance.Id });
        _eventsContext.SeekerHistory.Add(new SeekerHistory
        {
            ArrInstanceId = radarrInstance.Id,
            ExternalItemId = 514,
            ItemType = InstanceType.Radarr,
            CycleId = Guid.NewGuid(),
            ItemTitle = "Test Item",
            ConsecutiveMisses = 2,
        });

        AddTracker(radarrInstance.Id, eventId, externalItemId: 514);
        await _dataContext.SaveChangesAsync();
        await _eventsContext.SaveChangesAsync();

        StubCommandState(ArrCommandState.Completed);
        _arrClient.GetQueueItemsAsync(Arg.Any<ArrInstance>(), Arg.Any<int>())
            .Returns(new QueueListResponse());

        Task<SearchCommandStatus> publishTask = CaptureNextPublishedStatus();

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        await publishTask.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForTrackerCountAsync(0);

        // Assert
        SeekerHistory unit = await _eventsContext.SeekerHistory.AsNoTracking().SingleAsync(h => h.ArrInstanceId == radarrInstance.Id);
        unit.ConsecutiveMisses.ShouldBe(2);
    }

    [Fact]
    public async Task Replacement_search_without_a_cycle_leaves_units_and_cycles_untouched()
    {
        // Arrange: a replacement search's event carries no CycleId
        ArrInstance radarrInstance = TestDataContextFactory.AddRadarrInstance(_dataContext);
        Guid cycleId = Guid.NewGuid();
        Guid eventId = SetUpUnitOutcomeScenario(radarrInstance.Id, cycleId: null, externalItemId: 504);

        _eventsContext.SeekerCycles.Add(new SeekerCycle { Id = cycleId, ArrInstanceId = radarrInstance.Id, GrabbedUnits = 0 });

        AddTracker(radarrInstance.Id, eventId, externalItemId: 504);
        await _dataContext.SaveChangesAsync();
        await _eventsContext.SaveChangesAsync();

        StubCommandState(ArrCommandState.Completed);
        _arrClient.GetQueueItemsAsync(Arg.Any<ArrInstance>(), Arg.Any<int>())
            .Returns(new QueueListResponse
            {
                TotalRecords = 1,
                Records = [new QueueRecord { Id = 1, MovieId = 504, Title = "Grabbed.Movie", DownloadId = "HASH504", Protocol = "torrent", Status = "downloading" }]
            });

        Task<SearchCommandStatus> publishTask = CaptureNextPublishedStatus();

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        await publishTask.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForTrackerCountAsync(0);

        // Assert
        SeekerHistory unit = await _eventsContext.SeekerHistory.AsNoTracking().SingleAsync(h => h.ArrInstanceId == radarrInstance.Id);
        unit.LastOutcome.ShouldBeNull();
        unit.Grabs.ShouldBe(0);

        SeekerCycle cycle = await _eventsContext.SeekerCycles.AsNoTracking().SingleAsync(c => c.Id == cycleId);
        cycle.GrabbedUnits.ShouldBe(0);
    }

    [Fact]
    public async Task Dry_run_unit_rows_are_never_updated()
    {
        // Arrange: a dry-run unit with the same identifiers as the live search
        ArrInstance radarrInstance = TestDataContextFactory.AddRadarrInstance(_dataContext);
        Guid cycleId = Guid.NewGuid();
        Guid eventId = Guid.NewGuid();

        _eventsContext.Events.Add(new AppEvent
        {
            Id = eventId,
            EventType = EventType.SearchTriggered,
            Message = "Search triggered",
            Severity = EventSeverity.Information,
            CycleId = cycleId,
        });
        _eventsContext.SeekerCycles.Add(new SeekerCycle { Id = cycleId, ArrInstanceId = radarrInstance.Id, GrabbedUnits = 0 });
        _eventsContext.SeekerHistory.Add(new SeekerHistory
        {
            ArrInstanceId = radarrInstance.Id,
            ExternalItemId = 505,
            ItemType = InstanceType.Radarr,
            CycleId = cycleId,
            ItemTitle = "Dry Run Movie",
            IsDryRun = true,
        });

        AddTracker(radarrInstance.Id, eventId, externalItemId: 505);
        await _dataContext.SaveChangesAsync();
        await _eventsContext.SaveChangesAsync();

        StubCommandState(ArrCommandState.Completed);
        _arrClient.GetQueueItemsAsync(Arg.Any<ArrInstance>(), Arg.Any<int>())
            .Returns(new QueueListResponse
            {
                TotalRecords = 1,
                Records = [new QueueRecord { Id = 1, MovieId = 505, Title = "Grabbed.Movie", DownloadId = "HASH505", Protocol = "torrent", Status = "downloading" }]
            });

        Task<SearchCommandStatus> publishTask = CaptureNextPublishedStatus();

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        await publishTask.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForTrackerCountAsync(0);

        // Assert
        SeekerHistory unit = await _eventsContext.SeekerHistory.AsNoTracking().SingleAsync(h => h.ArrInstanceId == radarrInstance.Id);
        unit.LastOutcome.ShouldBeNull();
        unit.Grabs.ShouldBe(0);
    }

    [Fact]
    public async Task Delayed_outcome_from_an_older_cycle_leaves_a_re_searched_unit_untouched()
    {
        // Arrange: the unit was re-searched in a newer cycle before this old search's outcome arrived
        ArrInstance radarrInstance = TestDataContextFactory.AddRadarrInstance(_dataContext);
        Guid oldCycleId = Guid.NewGuid();
        Guid newerCycleId = Guid.NewGuid();
        Guid eventId = Guid.NewGuid();

        _eventsContext.Events.Add(new AppEvent
        {
            Id = eventId,
            EventType = EventType.SearchTriggered,
            Message = "Search triggered",
            Severity = EventSeverity.Information,
            CycleId = oldCycleId,
        });
        _eventsContext.SeekerCycles.Add(new SeekerCycle { Id = oldCycleId, ArrInstanceId = radarrInstance.Id, GrabbedUnits = 0 });
        _eventsContext.SeekerHistory.Add(new SeekerHistory
        {
            ArrInstanceId = radarrInstance.Id,
            ExternalItemId = 506,
            ItemType = InstanceType.Radarr,
            CycleId = newerCycleId,
            ItemTitle = "Test Item",
        });

        AddTracker(radarrInstance.Id, eventId, externalItemId: 506);
        await _dataContext.SaveChangesAsync();
        await _eventsContext.SaveChangesAsync();

        StubCommandState(ArrCommandState.Completed);
        _arrClient.GetQueueItemsAsync(Arg.Any<ArrInstance>(), Arg.Any<int>())
            .Returns(new QueueListResponse
            {
                TotalRecords = 1,
                Records = [new QueueRecord { Id = 1, MovieId = 506, Title = "Grabbed.Movie", DownloadId = "HASH506", Protocol = "torrent", Status = "downloading" }]
            });

        Task<SearchCommandStatus> publishTask = CaptureNextPublishedStatus();

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        await publishTask.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForTrackerCountAsync(0);

        // Assert
        SeekerHistory unit = await _eventsContext.SeekerHistory.AsNoTracking().SingleAsync(h => h.ArrInstanceId == radarrInstance.Id);
        unit.LastOutcome.ShouldBeNull();
        unit.Grabs.ShouldBe(1);

        SeekerCycle oldCycle = await _eventsContext.SeekerCycles.AsNoTracking().SingleAsync(c => c.Id == oldCycleId);
        oldCycle.GrabbedUnits.ShouldBe(1);
    }

    [Fact]
    public async Task Grabbed_outcome_with_no_matching_unit_row_still_updates_the_cycle()
    {
        // Arrange: the unit history row is gone (pruned, or never existed)
        ArrInstance radarrInstance = TestDataContextFactory.AddRadarrInstance(_dataContext);
        Guid cycleId = Guid.NewGuid();
        Guid eventId = Guid.NewGuid();

        _eventsContext.Events.Add(new AppEvent
        {
            Id = eventId,
            EventType = EventType.SearchTriggered,
            Message = "Search triggered",
            Severity = EventSeverity.Information,
            CycleId = cycleId,
        });
        _eventsContext.SeekerCycles.Add(new SeekerCycle { Id = cycleId, ArrInstanceId = radarrInstance.Id, GrabbedUnits = 0 });

        AddTracker(radarrInstance.Id, eventId, externalItemId: 507);
        await _dataContext.SaveChangesAsync();
        await _eventsContext.SaveChangesAsync();

        StubCommandState(ArrCommandState.Completed);
        _arrClient.GetQueueItemsAsync(Arg.Any<ArrInstance>(), Arg.Any<int>())
            .Returns(new QueueListResponse
            {
                TotalRecords = 1,
                Records = [new QueueRecord { Id = 1, MovieId = 507, Title = "Grabbed.Movie", DownloadId = "HASH507", Protocol = "torrent", Status = "downloading" }]
            });

        Task<SearchCommandStatus> publishTask = CaptureNextPublishedStatus();

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));
        await publishTask.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForTrackerCountAsync(0);

        // Assert
        (await _eventsContext.SeekerHistory.AnyAsync(h => h.ArrInstanceId == radarrInstance.Id)).ShouldBeFalse();

        SeekerCycle cycle = await _eventsContext.SeekerCycles.AsNoTracking().SingleAsync(c => c.Id == cycleId);
        cycle.GrabbedUnits.ShouldBe(1);
    }

    [Fact]
    public async Task Generic_exception_while_recording_the_outcome_is_logged_and_swallowed()
    {
        // Arrange: dispose the connection right as the outcome is published, so RecordUnitOutcomeAsync's own queries fail
        ArrInstance radarrInstance = TestDataContextFactory.AddRadarrInstance(_dataContext);
        Guid cycleId = Guid.NewGuid();
        Guid eventId = SetUpUnitOutcomeScenario(radarrInstance.Id, cycleId, externalItemId: 508);

        AddTracker(radarrInstance.Id, eventId, externalItemId: 508);
        await _dataContext.SaveChangesAsync();
        await _eventsContext.SaveChangesAsync();

        StubCommandState(ArrCommandState.Completed);
        _arrClient.GetQueueItemsAsync(Arg.Any<ArrInstance>(), Arg.Any<int>())
            .Returns(new QueueListResponse
            {
                TotalRecords = 1,
                Records = [new QueueRecord { Id = 1, MovieId = 508, Title = "Grabbed.Movie", DownloadId = "HASH508", Protocol = "torrent", Status = "downloading" }]
            });

        _eventPublisher.PublishSearchCompleted(
                Arg.Any<Guid>(), Arg.Any<SearchCommandStatus>(), Arg.Any<InstanceType>(), Arg.Any<string>(), Arg.Any<List<string>?>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => _eventsContext.Database.GetDbConnection().Dispose());

        // Act
        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));

        for (int attempt = 0; attempt < 200 && !_logger.HasLogContainingAtLeastOnce(LogLevel.Warning, "Failed to record the outcome of"); attempt++)
        {
            await Task.Delay(10);
            _timeProvider.Advance(TimeSpan.FromSeconds(60));
        }

        // Assert: the inner catch logged the warning, and the exception never escaped the background service
        _logger.HasLogContainingAtLeastOnce(LogLevel.Warning, "Failed to record the outcome of").ShouldBeTrue();
        _sut.ExecuteTask!.IsFaulted.ShouldBeFalse();
    }

    /// <summary>
    /// Adds the search-triggered event and matching unit history row RecordUnitOutcomeAsync reads to attribute an outcome.
    /// </summary>
    private Guid SetUpUnitOutcomeScenario(Guid arrInstanceId, Guid? cycleId, long externalItemId, int consecutiveMisses = 0, DateTimeOffset lastSearchedAt = default, DateTimeOffset? lastMissAt = null)
    {
        Guid eventId = Guid.NewGuid();

        _eventsContext.Events.Add(new AppEvent
        {
            Id = eventId,
            EventType = EventType.SearchTriggered,
            Message = "Search triggered",
            Severity = EventSeverity.Information,
            CycleId = cycleId,
        });

        if (cycleId.HasValue)
        {
            _eventsContext.SeekerCycles.Add(new SeekerCycle { Id = cycleId.Value, ArrInstanceId = arrInstanceId, GrabbedUnits = 0 });
        }

        _eventsContext.SeekerHistory.Add(new SeekerHistory
        {
            ArrInstanceId = arrInstanceId,
            ExternalItemId = externalItemId,
            ItemType = InstanceType.Radarr,
            CycleId = cycleId ?? Guid.NewGuid(),
            ItemTitle = "Test Item",
            ConsecutiveMisses = consecutiveMisses,
            LastSearchedAt = lastSearchedAt,
            LastMissAt = lastMissAt,
        });

        return eventId;
    }

    #endregion

    private SeekerCommandTracker Tracker(
        Guid arrInstanceId,
        long externalItemId,
        long episodeId = 0,
        int seasonNumber = 0,
        string itemTitle = "Test Item") =>
        new()
        {
            ArrInstanceId = arrInstanceId,
            CommandId = 1,
            EventId = Guid.NewGuid(),
            ExternalItemId = externalItemId,
            EpisodeId = episodeId,
            ItemTitle = itemTitle,
            SeasonNumber = seasonNumber,
            Status = SearchCommandStatus.Pending,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
        };

    /// <summary>
    /// Runs one monitor cycle over a completed command and returns the titles it attributed.
    /// </summary>
    private async Task<List<string>?> CaptureGrabbedItemsAsync(SeekerCommandTracker tracker, params QueueRecord[] records)
    {
        _eventsContext.SeekerCommandTrackers.Add(tracker);
        await _dataContext.SaveChangesAsync();
        await _eventsContext.SaveChangesAsync();

        StubCommandState(ArrCommandState.Completed, tracker.CommandId);

        _arrClient.GetQueueItemsAsync(Arg.Any<ArrInstance>(), Arg.Any<int>())
            .Returns(new QueueListResponse { TotalRecords = records.Length, Records = [.. records] });

        TaskCompletionSource<List<string>?> publishTcs = new();
        _eventPublisher.PublishSearchCompleted(
                Arg.Any<Guid>(), Arg.Any<SearchCommandStatus>(), Arg.Any<InstanceType>(), Arg.Any<string>(), Arg.Any<List<string>?>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => publishTcs.TrySetResult(ci.ArgAt<List<string>?>(4)));

        await _sut.StartAsync(_cts.Token);
        _timeProvider.Advance(TimeSpan.FromSeconds(11));

        return await publishTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private void StubCommandState(ArrCommandState state, long commandId = 1)
    {
        _arrClient.GetCommandsAsync(Arg.Any<ArrInstance>())
            .Returns([new ArrCommandStatus(commandId, state, null)]);
        _arrClient.GetCommandStatusAsync(Arg.Any<ArrInstance>(), Arg.Any<long>())
            .Returns(new ArrCommandStatus(commandId, state, null));
    }

    private void AddTracker(
        Guid arrInstanceId,
        Guid eventId,
        long commandId = 1,
        TimeSpan? age = null,
        SearchCommandStatus status = SearchCommandStatus.Pending,
        long externalItemId = 300)
    {
        _eventsContext.SeekerCommandTrackers.Add(new SeekerCommandTracker
        {
            ArrInstanceId = arrInstanceId,
            CommandId = commandId,
            EventId = eventId,
            ExternalItemId = externalItemId,
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

    private Task<SearchCommandStatus> CaptureNextPublishedStatus(out Task savedAfterPublish)
    {
        TaskCompletionSource saved = new();
        savedAfterPublish = saved.Task;

        TaskCompletionSource<SearchCommandStatus> tcs = new();

        _eventPublisher.PublishSearchCompleted(
                Arg.Any<Guid>(), Arg.Any<SearchCommandStatus>(), Arg.Any<InstanceType>(), Arg.Any<string>(), Arg.Any<List<string>?>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci =>
            {
                // armed here so the save before the publish cannot complete it
                _eventsContext.SavedChanges += (_, _) => saved.TrySetResult();
                _eventsContext.SaveChangesFailed += (_, _) => saved.TrySetResult();
                tcs.TrySetResult(ci.ArgAt<SearchCommandStatus>(1));
            });

        return tcs.Task;
    }

    private Task<SearchCommandStatus> CaptureNextPublishedStatus() => CaptureNextPublishedStatus(out _);

    private Task CaptureNthPoll(int count)
    {
        TaskCompletionSource tcs = new();
        int seen = 0;

        _arrClient
            .When(client => client.GetCommandsAsync(Arg.Any<ArrInstance>()))
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
            _timeProvider.Advance(TimeSpan.FromSeconds(60));
        }

        await signal.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
