using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Tests.Features.Jobs.TestHelpers;
using Cleanuparr.Persistence.Models.State;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;
using SeekerJob = Cleanuparr.Infrastructure.Features.Jobs.Seeker;

namespace Cleanuparr.Infrastructure.Tests.Features.Jobs.Integration;

[Collection(IntegrationTestCollection.Name)]
public class SeekerIntegrationTests : IDisposable
{
    private readonly IntegrationTestFixture _fixture;

    public SeekerIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    public void Dispose()
    {
        Striker.RecurringHashes.Clear();
    }

    private SeekerJob CreateSut()
    {
        var environment = Substitute.For<IHostingEnvironment>();
        environment.EnvironmentName.Returns("Development");

        return new SeekerJob(
            Substitute.For<ILogger<SeekerJob>>(),
            _fixture.DataContext,
            Substitute.For<IRadarrClient>(),
            Substitute.For<ISonarrClient>(),
            _fixture.ArrClientFactory,
            _fixture.ArrQueueIterator,
            _fixture.EventPublisher,
            _fixture.DryRunInterceptor,
            environment,
            _fixture.TimeProvider,
            _fixture.HubContext);
    }

    [Fact]
    public async Task ReplacementSearch_TriggersSearch_SavesEvent_SendsNotification_DequesItem()
    {
        // Arrange
        var instance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        // Add a replacement search item to the queue
        _fixture.DataContext.SearchQueue.Add(new SearchQueueItem
        {
            ArrInstanceId = instance.Id,
            ItemId = 42,
            Title = "Test.Movie.2024.1080p",
        });
        await _fixture.DataContext.SaveChangesAsync();

        // Mock arr client to return command IDs on search
        _fixture.ArrClient.SearchItemAsync(Arg.Any<Cleanuparr.Persistence.Models.Configuration.Arr.ArrInstance>(), Arg.Any<SearchItem>())
            .Returns(100L);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert: Full SearchTriggered event property verification
        var events = await _fixture.EventsContext.Events.ToListAsync();
        events.Count.ShouldBe(1);

        var searchEvent = events.First(e => e.EventType == EventType.SearchTriggered);
        searchEvent.Message.ShouldBe("Search triggered for Test.Movie.2024.1080p");
        searchEvent.Severity.ShouldBe(EventSeverity.Information);
        searchEvent.JobRunId.ShouldBe(_fixture.JobRunId);
        searchEvent.ArrInstanceId.ShouldBe(instance.Id);
        searchEvent.DownloadClientId.ShouldBeNull();
        searchEvent.IsDryRun.ShouldBe(false);
        searchEvent.SearchStatus.ShouldBe(SearchCommandStatus.Pending);
        searchEvent.CompletedAt.ShouldBeNull();
        searchEvent.CycleId.ShouldBeNull();
        searchEvent.StrikeId.ShouldBeNull();
        searchEvent.TrackingId.ShouldBeNull();
        searchEvent.Data.ShouldBeNull();

        // Assert: SearchEventData was created with correct properties
        var searchData = await _fixture.EventsContext.SearchEventData.ToListAsync();
        searchData.Count.ShouldBe(1);
        searchData[0].AppEventId.ShouldBe(searchEvent.Id);
        searchData[0].SearchType.ShouldBe(SeekerSearchType.Replacement);
        searchData[0].SearchReason.ShouldBe(SeekerSearchReason.Replacement);
        searchData[0].ItemTitle.ShouldBe("Test.Movie.2024.1080p");
        searchData[0].GrabbedItems.ShouldBeEmpty();

        // Assert: Notification was sent
        await _fixture.NotificationPublisher.Received(1).NotifySearchTriggered(
            "Test.Movie.2024.1080p",
            SeekerSearchType.Replacement,
            SeekerSearchReason.Replacement);

        // Assert: Item was dequeued from SearchQueue
        var remainingItems = await _fixture.DataContext.SearchQueue.CountAsync();
        remainingItems.ShouldBe(0);

        // Assert: Command tracker was saved for monitoring
        var trackers = await _fixture.DataContext.SeekerCommandTrackers.ToListAsync();
        trackers.Count.ShouldBe(1);
        trackers[0].CommandId.ShouldBe(100L);
    }

    [Fact]
    public async Task SearchDisabled_DoesNothing()
    {
        // Arrange
        var seekerConfig = await _fixture.DataContext.SeekerConfigs.FirstAsync();
        seekerConfig.SearchEnabled = false;
        await _fixture.DataContext.SaveChangesAsync();

        TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        // Add a search queue item that should NOT be processed
        var instance = await _fixture.DataContext.ArrInstances.FirstAsync();
        _fixture.DataContext.SearchQueue.Add(new SearchQueueItem
        {
            ArrInstanceId = instance.Id,
            ItemId = 99,
            Title = "Should.Not.Search",
        });
        await _fixture.DataContext.SaveChangesAsync();

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert: No search triggered, no events, no notifications
        await _fixture.ArrClient.DidNotReceive().SearchItemAsync(
            Arg.Any<Cleanuparr.Persistence.Models.Configuration.Arr.ArrInstance>(),
            Arg.Any<SearchItem>());

        var events = await _fixture.EventsContext.Events.ToListAsync();
        events.ShouldBeEmpty();

        var searchData = await _fixture.EventsContext.SearchEventData.ToListAsync();
        searchData.ShouldBeEmpty();

        await _fixture.NotificationPublisher.DidNotReceive().NotifySearchTriggered(
            Arg.Any<string>(), Arg.Any<SeekerSearchType>(), Arg.Any<SeekerSearchReason>());

        // Item should still be in the queue (not processed)
        var remainingItems = await _fixture.DataContext.SearchQueue.CountAsync();
        remainingItems.ShouldBe(1);
    }

    [Fact]
    public async Task DryRun_TriggersSearch_ButDoesNotDequeue()
    {
        // Arrange
        _fixture.DryRunInterceptor.IsDryRunEnabled().Returns(true);

        var instance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);

        _fixture.DataContext.SearchQueue.Add(new SearchQueueItem
        {
            ArrInstanceId = instance.Id,
            ItemId = 55,
            Title = "DryRun.Movie.2024",
        });
        await _fixture.DataContext.SaveChangesAsync();

        _fixture.ArrClient.SearchItemAsync(
            Arg.Any<Cleanuparr.Persistence.Models.Configuration.Arr.ArrInstance>(),
            Arg.Any<SearchItem>())
            .Returns(200L);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert: Full SearchTriggered event with IsDryRun = true
        var events = await _fixture.EventsContext.Events.ToListAsync();
        events.Count.ShouldBe(1);

        var searchEvent = events.First(e => e.EventType == EventType.SearchTriggered);
        searchEvent.Message.ShouldBe("Search triggered for DryRun.Movie.2024");
        searchEvent.Severity.ShouldBe(EventSeverity.Information);
        searchEvent.JobRunId.ShouldBe(_fixture.JobRunId);
        searchEvent.ArrInstanceId.ShouldBe(instance.Id);
        searchEvent.IsDryRun.ShouldBe(true);
        searchEvent.SearchStatus.ShouldBe(SearchCommandStatus.Pending);
        searchEvent.CompletedAt.ShouldBeNull();
        searchEvent.CycleId.ShouldBeNull();
        searchEvent.StrikeId.ShouldBeNull();
        searchEvent.Data.ShouldBeNull();

        // Assert: SearchEventData created
        var searchData = await _fixture.EventsContext.SearchEventData.ToListAsync();
        searchData.Count.ShouldBe(1);
        searchData[0].ItemTitle.ShouldBe("DryRun.Movie.2024");
        searchData[0].SearchType.ShouldBe(SeekerSearchType.Replacement);
        searchData[0].SearchReason.ShouldBe(SeekerSearchReason.Replacement);
        searchData[0].GrabbedItems.ShouldBeEmpty();

        // Assert: Item remains in queue (dry run doesn't dequeue)
        var remainingItems = await _fixture.DataContext.SearchQueue.CountAsync();
        remainingItems.ShouldBe(1);

        // Assert: No command tracker saved (dry run)
        var trackers = await _fixture.DataContext.SeekerCommandTrackers.ToListAsync();
        trackers.ShouldBeEmpty();
    }
}
