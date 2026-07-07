using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events;
using Cleanuparr.Infrastructure.Tests.Features.Jobs.TestHelpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Events;
using Cleanuparr.Persistence.Models.State;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Events;

/// <summary>
/// Exercises the EventCleanupService prune logic against a real SQLite context
/// (the InMemory provider cannot run ExecuteDeleteAsync).
/// </summary>
public class EventCleanupLogicTests : IDisposable
{
    private readonly EventsContext _context;
    private readonly EventCleanupService _service;

    public EventCleanupLogicTests()
    {
        _context = TestEventsContextFactory.Create();
        _service = new EventCleanupService(
            Substitute.For<ILogger<EventCleanupService>>(),
            Substitute.For<IServiceScopeFactory>());
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task PruneEventsAsync_DeletesEventsBeyondRetention()
    {
        _context.Events.Add(new AppEvent
        {
            EventType = EventType.StrikeReset,
            Message = "stale",
            Severity = EventSeverity.Information,
            Timestamp = DateTimeOffset.UtcNow.AddDays(-400),
        });
        _context.Events.Add(new AppEvent
        {
            EventType = EventType.StrikeReset,
            Message = "fresh",
            Severity = EventSeverity.Information,
            Timestamp = DateTimeOffset.UtcNow.AddDays(-10),
        });
        await _context.SaveChangesAsync();

        await _service.PruneEventsAsync(_context, retentionDays: 365);

        List<AppEvent> remaining = await _context.Events.ToListAsync();
        remaining.Count.ShouldBe(1);
        remaining[0].Message.ShouldBe("fresh");
    }

    [Fact]
    public async Task DeleteResolvedManualEventsAsync_KeepsRecentlyResolvedOldEvents()
    {
        DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddDays(-30);

        // Created long ago but resolved just now — must survive so the publish cooldown still sees it.
        ManualEvent freshlyResolved = new()
        {
            Type = ManualEventType.RecurringDownload,
            Message = "fresh",
            Severity = EventSeverity.Warning,
            Timestamp = DateTimeOffset.UtcNow.AddDays(-40),
            IsResolved = true,
            ResolvedAt = DateTimeOffset.UtcNow,
        };
        // Created and resolved long ago — safe to delete.
        ManualEvent longResolved = new()
        {
            Type = ManualEventType.SearchNotTriggered,
            Message = "stale",
            Severity = EventSeverity.Warning,
            Timestamp = DateTimeOffset.UtcNow.AddDays(-40),
            IsResolved = true,
            ResolvedAt = DateTimeOffset.UtcNow.AddDays(-35),
        };
        // Old but still unresolved — never deleted here.
        ManualEvent unresolved = new()
        {
            Type = ManualEventType.RecurringDownload,
            Message = "open",
            Severity = EventSeverity.Warning,
            Timestamp = DateTimeOffset.UtcNow.AddDays(-40),
            IsResolved = false,
        };
        _context.ManualEvents.AddRange(freshlyResolved, longResolved, unresolved);
        await _context.SaveChangesAsync();

        await _service.DeleteResolvedManualEventsAsync(_context, cutoff);

        List<string> remaining = await _context.ManualEvents.Select(e => e.Message).ToListAsync();
        remaining.ShouldContain("fresh");
        remaining.ShouldContain("open");
        remaining.ShouldNotContain("stale");
    }

    [Fact]
    public async Task PruneJobRunsAsync_DeletesOnlyOldCompletedUnreferencedRuns()
    {
        DateTimeOffset oldTime = DateTimeOffset.UtcNow.AddDays(-40);
        DateTimeOffset recentTime = DateTimeOffset.UtcNow.AddDays(-5);

        JobRun unreferenced = new() { Id = Guid.NewGuid(), Type = JobType.QueueCleaner, StartedAt = oldTime, CompletedAt = oldTime };
        JobRun referencedByStrike = new() { Id = Guid.NewGuid(), Type = JobType.QueueCleaner, StartedAt = oldTime, CompletedAt = oldTime };
        JobRun referencedByEvent = new() { Id = Guid.NewGuid(), Type = JobType.QueueCleaner, StartedAt = oldTime, CompletedAt = oldTime };
        JobRun referencedByManualEvent = new() { Id = Guid.NewGuid(), Type = JobType.QueueCleaner, StartedAt = oldTime, CompletedAt = oldTime };
        JobRun recent = new() { Id = Guid.NewGuid(), Type = JobType.QueueCleaner, StartedAt = recentTime, CompletedAt = recentTime };
        JobRun incomplete = new() { Id = Guid.NewGuid(), Type = JobType.QueueCleaner, StartedAt = oldTime, CompletedAt = null };
        _context.JobRuns.AddRange(unreferenced, referencedByStrike, referencedByEvent, referencedByManualEvent, recent, incomplete);

        DownloadItem item = new() { DownloadId = "h1", Title = "t1" };
        _context.DownloadItems.Add(item);
        _context.Strikes.Add(new Strike { DownloadItemId = item.Id, JobRunId = referencedByStrike.Id, Type = StrikeType.Stalled });
        _context.Events.Add(new AppEvent
        {
            EventType = EventType.StalledStrike,
            Message = "e",
            Severity = EventSeverity.Important,
            JobRunId = referencedByEvent.Id,
        });
        _context.ManualEvents.Add(new ManualEvent
        {
            Type = ManualEventType.RecurringDownload,
            Message = "m",
            Severity = EventSeverity.Important,
            JobRunId = referencedByManualEvent.Id,
        });
        await _context.SaveChangesAsync();

        await _service.PruneJobRunsAsync(_context, DateTimeOffset.UtcNow.AddDays(-30));

        List<Guid> remaining = await _context.JobRuns.Select(j => j.Id).ToListAsync();
        remaining.ShouldNotContain(unreferenced.Id);
        remaining.ShouldContain(referencedByStrike.Id);
        remaining.ShouldContain(referencedByEvent.Id);
        remaining.ShouldContain(referencedByManualEvent.Id);
        remaining.ShouldContain(recent.Id);
        remaining.ShouldContain(incomplete.Id);
    }
}
