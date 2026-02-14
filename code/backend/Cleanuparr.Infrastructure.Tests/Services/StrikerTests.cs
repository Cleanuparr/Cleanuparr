using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Features.Notifications;
using Cleanuparr.Infrastructure.Hubs;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Services;

public class StrikerTests : IDisposable
{
    private readonly EventsContext _strikerContext;
    private readonly ILogger<Striker> _logger;
    private readonly EventPublisher _eventPublisher;
    private readonly Striker _striker;

    public StrikerTests()
    {
        _strikerContext = CreateInMemoryEventsContext();
        _logger = Substitute.For<ILogger<Striker>>();

        // Create EventPublisher with mocked dependencies
        var eventsContext = CreateInMemoryEventsContext();
        var hubContext = Substitute.For<IHubContext<AppHub>>();
        var hubClients = Substitute.For<IHubClients>();
        var clientProxy = Substitute.For<IClientProxy>();
        hubContext.Clients.Returns(hubClients);
        hubClients.All.Returns(clientProxy);

        var eventLogger = Substitute.For<ILogger<EventPublisher>>();
        var notificationPublisher = Substitute.For<INotificationPublisher>();
        var dryRunInterceptor = Substitute.For<IDryRunInterceptor>();

        // Configure dry run interceptor to just complete the task (we don't need actual DB saves in tests)
        dryRunInterceptor
            .InterceptAsync(Arg.Any<Delegate>(), Arg.Any<object[]>())
            .Returns(Task.CompletedTask);

        _eventPublisher = new EventPublisher(
            eventsContext,
            hubContext,
            eventLogger,
            notificationPublisher,
            dryRunInterceptor);

        _striker = new Striker(_logger, _strikerContext, _eventPublisher);

        // Clear static state before each test
        Striker.RecurringHashes.Clear();

        // Set up required JobRunId for tests
        ContextProvider.SetJobRunId(Guid.NewGuid());

        // Set up required context for recurring item events and FailedImport strikes
        ContextProvider.Set(nameof(InstanceType), (object)InstanceType.Sonarr);
        ContextProvider.Set(ContextProvider.Keys.ArrInstanceUrl, new Uri("http://localhost:8989"));
        ContextProvider.Set(new QueueRecord
        {
            Title = "Test Item",
            DownloadId = "test-download-id",
            Protocol = "torrent",
            Id = 1,
            StatusMessages = []
        });
    }

    private static EventsContext CreateInMemoryEventsContext()
    {
        var options = new DbContextOptionsBuilder<EventsContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new EventsContext(options);
    }

    public void Dispose()
    {
        _strikerContext.Dispose();
        Striker.RecurringHashes.Clear();
    }

    [Fact]
    public async Task StrikeAndCheckLimit_FirstStrike_ReturnsFalse()
    {
        // Arrange
        const string hash = "abc123";
        const string itemName = "Test Item";
        const ushort maxStrikes = 3;

        // Act
        var result = await _striker.StrikeAndCheckLimit(hash, itemName, maxStrikes, StrikeType.Stalled);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task StrikeAndCheckLimit_ReachesMaxStrikes_ReturnsTrue()
    {
        // Arrange
        const string hash = "abc123";
        const string itemName = "Test Item";
        const ushort maxStrikes = 3;

        // Act - Strike 3 times
        await _striker.StrikeAndCheckLimit(hash, itemName, maxStrikes, StrikeType.Stalled);
        await _striker.StrikeAndCheckLimit(hash, itemName, maxStrikes, StrikeType.Stalled);
        var result = await _striker.StrikeAndCheckLimit(hash, itemName, maxStrikes, StrikeType.Stalled);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task StrikeAndCheckLimit_ExceedsMaxStrikes_ReturnsTrue_AndAddsToRecurringHashes()
    {
        // Arrange
        const string hash = "ABC123";
        const string itemName = "Recurring Item";
        const ushort maxStrikes = 2;

        // Act - Strike 3 times (exceeds max of 2)
        await _striker.StrikeAndCheckLimit(hash, itemName, maxStrikes, StrikeType.Stalled);
        await _striker.StrikeAndCheckLimit(hash, itemName, maxStrikes, StrikeType.Stalled);
        var result = await _striker.StrikeAndCheckLimit(hash, itemName, maxStrikes, StrikeType.Stalled);

        // Assert
        result.ShouldBeTrue();
        Striker.RecurringHashes.ShouldContainKey(hash.ToLowerInvariant());
    }

    [Fact]
    public async Task StrikeAndCheckLimit_DifferentStrikeTypes_TrackedSeparately()
    {
        // Arrange
        const string hash = "abc123";
        const string itemName = "Test Item";
        const ushort maxStrikes = 2;

        // Act - Strike with different types
        var stalledResult1 = await _striker.StrikeAndCheckLimit(hash, itemName, maxStrikes, StrikeType.Stalled);
        var slowSpeedResult1 = await _striker.StrikeAndCheckLimit(hash, itemName, maxStrikes, StrikeType.SlowSpeed);
        var stalledResult2 = await _striker.StrikeAndCheckLimit(hash, itemName, maxStrikes, StrikeType.Stalled);
        var slowSpeedResult2 = await _striker.StrikeAndCheckLimit(hash, itemName, maxStrikes, StrikeType.SlowSpeed);

        // Assert - Both should reach max independently
        stalledResult1.ShouldBeFalse();
        slowSpeedResult1.ShouldBeFalse();
        stalledResult2.ShouldBeTrue(); // 2nd stalled strike = maxStrikes
        slowSpeedResult2.ShouldBeTrue(); // 2nd slow speed strike = maxStrikes
    }

    [Fact]
    public async Task StrikeAndCheckLimit_SameHash_AccumulatesStrikes()
    {
        // Arrange
        const string hash = "abc123";
        const string itemName = "Test Item";
        const ushort maxStrikes = 5;

        // Act - Strike 4 times
        var result1 = await _striker.StrikeAndCheckLimit(hash, itemName, maxStrikes, StrikeType.Stalled);
        var result2 = await _striker.StrikeAndCheckLimit(hash, itemName, maxStrikes, StrikeType.Stalled);
        var result3 = await _striker.StrikeAndCheckLimit(hash, itemName, maxStrikes, StrikeType.Stalled);
        var result4 = await _striker.StrikeAndCheckLimit(hash, itemName, maxStrikes, StrikeType.Stalled);

        // Assert - None should trigger removal yet (need 5)
        result1.ShouldBeFalse();
        result2.ShouldBeFalse();
        result3.ShouldBeFalse();
        result4.ShouldBeFalse();

        // 5th strike should trigger removal
        var result5 = await _striker.StrikeAndCheckLimit(hash, itemName, maxStrikes, StrikeType.Stalled);
        result5.ShouldBeTrue();
    }

    [Fact]
    public async Task ResetStrikeAsync_ClearsStrikeCount()
    {
        // Arrange
        const string hash = "abc123";
        const string itemName = "Test Item";
        const ushort maxStrikes = 3;

        // Strike twice
        await _striker.StrikeAndCheckLimit(hash, itemName, maxStrikes, StrikeType.Stalled);
        await _striker.StrikeAndCheckLimit(hash, itemName, maxStrikes, StrikeType.Stalled);

        // Act - Reset strikes
        await _striker.ResetStrikeAsync(hash, itemName, StrikeType.Stalled);

        // Assert - Next strike should be treated as first (returns false)
        var result = await _striker.StrikeAndCheckLimit(hash, itemName, maxStrikes, StrikeType.Stalled);
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task ResetStrikeAsync_OnlyResetsSpecifiedType()
    {
        // Arrange
        const string hash = "abc123";
        const string itemName = "Test Item";
        const ushort maxStrikes = 2;

        // Strike with both types
        await _striker.StrikeAndCheckLimit(hash, itemName, maxStrikes, StrikeType.Stalled);
        await _striker.StrikeAndCheckLimit(hash, itemName, maxStrikes, StrikeType.SlowSpeed);

        // Act - Reset only Stalled strikes
        await _striker.ResetStrikeAsync(hash, itemName, StrikeType.Stalled);

        // Assert - Stalled should be reset (1st strike = false), SlowSpeed should continue (2nd strike = true)
        var stalledResult = await _striker.StrikeAndCheckLimit(hash, itemName, maxStrikes, StrikeType.Stalled);
        var slowSpeedResult = await _striker.StrikeAndCheckLimit(hash, itemName, maxStrikes, StrikeType.SlowSpeed);

        stalledResult.ShouldBeFalse(); // Reset, so this is strike #1
        slowSpeedResult.ShouldBeTrue(); // Not reset, so this is strike #2 = maxStrikes
    }

    [Fact]
    public async Task StrikeAndCheckLimit_ZeroMaxStrikes_ReturnsFalse()
    {
        // Arrange
        const string hash = "abc123";
        const string itemName = "Test Item";
        const ushort maxStrikes = 0;

        // Act
        var result = await _striker.StrikeAndCheckLimit(hash, itemName, maxStrikes, StrikeType.Stalled);

        // Assert - Should return false immediately without striking
        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData((ushort)2, 0, false)]  // Strike 1, max 2 -> below limit (1 < 2)
    [InlineData((ushort)2, 1, true)]   // Strike 2, max 2 -> at limit (2 >= 2)
    [InlineData((ushort)3, 1, false)]  // Strike 2, max 3 -> below limit (2 < 3)
    [InlineData((ushort)3, 2, true)]   // Strike 3, max 3 -> at limit (3 >= 3)
    [InlineData((ushort)1, 0, true)]   // Strike 1, max 1 -> at limit (1 >= 1)
    public async Task StrikeAndCheckLimit_BoundaryConditions(ushort maxStrikes, int preStrikes, bool expectedResult)
    {
        // Arrange
        const string hash = "boundary-test";
        const string itemName = "Boundary Test Item";

        // Pre-strike
        for (int i = 0; i < preStrikes; i++)
        {
            await _striker.StrikeAndCheckLimit(hash, itemName, maxStrikes, StrikeType.Stalled);
        }

        // Act
        var result = await _striker.StrikeAndCheckLimit(hash, itemName, maxStrikes, StrikeType.Stalled);

        // Assert
        result.ShouldBe(expectedResult);
    }

    [Theory]
    [InlineData(StrikeType.Stalled)]
    [InlineData(StrikeType.DownloadingMetadata)]
    [InlineData(StrikeType.FailedImport)]
    [InlineData(StrikeType.SlowSpeed)]
    [InlineData(StrikeType.SlowTime)]
    public async Task StrikeAndCheckLimit_AllStrikeTypes_WorkCorrectly(StrikeType strikeType)
    {
        // Arrange
        const string hash = "type-test";
        const string itemName = "Type Test Item";
        const ushort maxStrikes = 2;

        // Act
        var result1 = await _striker.StrikeAndCheckLimit(hash, itemName, maxStrikes, strikeType);
        var result2 = await _striker.StrikeAndCheckLimit(hash, itemName, maxStrikes, strikeType);

        // Assert
        result1.ShouldBeFalse();
        result2.ShouldBeTrue();
    }

    [Fact]
    public async Task StrikeAndCheckLimit_DifferentHashes_TrackedSeparately()
    {
        // Arrange
        const string hash1 = "hash1";
        const string hash2 = "hash2";
        const string itemName = "Test Item";
        const ushort maxStrikes = 2;

        // Act - Strike hash1 twice, hash2 once
        await _striker.StrikeAndCheckLimit(hash1, itemName, maxStrikes, StrikeType.Stalled);
        await _striker.StrikeAndCheckLimit(hash2, itemName, maxStrikes, StrikeType.Stalled);
        var hash1Result = await _striker.StrikeAndCheckLimit(hash1, itemName, maxStrikes, StrikeType.Stalled);
        var hash2Result = await _striker.StrikeAndCheckLimit(hash2, itemName, maxStrikes, StrikeType.Stalled);

        // Assert
        hash1Result.ShouldBeTrue();  // hash1 reached max (2 strikes)
        hash2Result.ShouldBeTrue();  // hash2 reached max (2 strikes)
    }

    [Fact]
    public async Task ResetStrikeAsync_NonExistentStrike_DoesNotThrow()
    {
        // Arrange
        const string hash = "never-struck";
        const string itemName = "Never Struck Item";

        // Act & Assert - Should not throw
        await Should.NotThrowAsync(async () =>
            await _striker.ResetStrikeAsync(hash, itemName, StrikeType.Stalled));
    }

    [Fact]
    public async Task StrikeAndCheckLimit_RecurringItem_OnlyAddedOnceToRecurringHashes()
    {
        // Arrange
        const string hash = "recurring-hash";
        const string itemName = "Recurring Item";
        const ushort maxStrikes = 1;

        // Act - Strike multiple times past the limit
        await _striker.StrikeAndCheckLimit(hash, itemName, maxStrikes, StrikeType.Stalled);
        await _striker.StrikeAndCheckLimit(hash, itemName, maxStrikes, StrikeType.Stalled);
        await _striker.StrikeAndCheckLimit(hash, itemName, maxStrikes, StrikeType.Stalled);
        await _striker.StrikeAndCheckLimit(hash, itemName, maxStrikes, StrikeType.Stalled);

        // Assert - Hash should only appear once in RecurringHashes
        Striker.RecurringHashes.Count.ShouldBe(1);
        Striker.RecurringHashes.ShouldContainKey(hash.ToLowerInvariant());
    }

    [Fact]
    public async Task StrikeAndCheckLimit_CreatesNewStrikeRowForEachStrike()
    {
        // Arrange
        const string hash = "strike-rows-test";
        const string itemName = "Test Item";
        const ushort maxStrikes = 5;

        // Act - Strike 3 times
        await _striker.StrikeAndCheckLimit(hash, itemName, maxStrikes, StrikeType.Stalled);
        await _striker.StrikeAndCheckLimit(hash, itemName, maxStrikes, StrikeType.Stalled);
        await _striker.StrikeAndCheckLimit(hash, itemName, maxStrikes, StrikeType.Stalled);

        // Assert - Should have 3 strike rows
        var downloadItem = await _strikerContext.DownloadItems.FirstOrDefaultAsync(d => d.DownloadId == hash);
        downloadItem.ShouldNotBeNull();

        var strikeCount = await _strikerContext.Strikes
            .CountAsync(s => s.DownloadItemId == downloadItem.Id && s.Type == StrikeType.Stalled);
        strikeCount.ShouldBe(3);
    }

    [Fact]
    public async Task StrikeAndCheckLimit_StoresTitleOnDownloadItem()
    {
        // Arrange
        const string hash = "title-test";
        const string itemName = "My Movie Title 2024";
        const ushort maxStrikes = 3;

        // Act
        await _striker.StrikeAndCheckLimit(hash, itemName, maxStrikes, StrikeType.Stalled);

        // Assert
        var downloadItem = await _strikerContext.DownloadItems.FirstOrDefaultAsync(d => d.DownloadId == hash);
        downloadItem.ShouldNotBeNull();
        downloadItem.Title.ShouldBe(itemName);
    }

    [Fact]
    public async Task StrikeAndCheckLimit_UpdatesTitleOnDownloadItem_WhenTitleChanges()
    {
        // Arrange
        const string hash = "title-update-test";
        const string initialTitle = "Initial Title";
        const string updatedTitle = "Updated Title";
        const ushort maxStrikes = 5;

        // Act - Strike with initial title
        await _striker.StrikeAndCheckLimit(hash, initialTitle, maxStrikes, StrikeType.Stalled);

        // Strike with updated title
        await _striker.StrikeAndCheckLimit(hash, updatedTitle, maxStrikes, StrikeType.Stalled);

        // Assert - Title should be updated
        var downloadItem = await _strikerContext.DownloadItems.FirstOrDefaultAsync(d => d.DownloadId == hash);
        downloadItem.ShouldNotBeNull();
        downloadItem.Title.ShouldBe(updatedTitle);
    }
}
