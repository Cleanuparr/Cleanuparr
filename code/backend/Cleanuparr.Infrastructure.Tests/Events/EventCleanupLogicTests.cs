using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events;
using Cleanuparr.Infrastructure.Tests.Features.Jobs.TestHelpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Events;
using Cleanuparr.Persistence.Models.State;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
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
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly EventsContext _context;
    private readonly DataContext _dataContext;
    private readonly ServiceProvider _serviceProvider;
    private readonly EventCleanupService _service;

    public EventCleanupLogicTests()
    {
        _context = TestEventsContextFactory.Create();
        _dataContext = TestDataContextFactory.Create();

        ServiceCollection services = new();
        services.AddSingleton(_context);
        services.AddSingleton(_dataContext);
        _serviceProvider = services.BuildServiceProvider();

        _service = new EventCleanupService(
            Substitute.For<ILogger<EventCleanupService>>(),
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            new FakeTimeProvider(Now));
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        _dataContext.Dispose();
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
            Timestamp = Now.AddDays(-400),
        });
        _context.Events.Add(new AppEvent
        {
            EventType = EventType.StrikeReset,
            Message = "fresh",
            Severity = EventSeverity.Information,
            Timestamp = Now.AddDays(-10),
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
        DateTimeOffset cutoff = Now.AddDays(-30);

        // Created long ago but resolved just now — must survive so the publish cooldown still sees it.
        ManualEvent freshlyResolved = new()
        {
            Type = ManualEventType.RecurringDownload,
            Message = "fresh",
            Severity = EventSeverity.Warning,
            Timestamp = Now.AddDays(-40),
            IsResolved = true,
            ResolvedAt = Now,
        };
        // Created and resolved long ago — safe to delete.
        ManualEvent longResolved = new()
        {
            Type = ManualEventType.SearchNotTriggered,
            Message = "stale",
            Severity = EventSeverity.Warning,
            Timestamp = Now.AddDays(-40),
            IsResolved = true,
            ResolvedAt = Now.AddDays(-35),
        };
        // Old but still unresolved — never deleted here.
        ManualEvent unresolved = new()
        {
            Type = ManualEventType.RecurringDownload,
            Message = "open",
            Severity = EventSeverity.Warning,
            Timestamp = Now.AddDays(-40),
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
        DateTimeOffset oldTime = Now.AddDays(-40);
        DateTimeOffset recentTime = Now.AddDays(-5);

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

        await _service.PruneJobRunsAsync(_context, Now.AddDays(-30));

        List<Guid> remaining = await _context.JobRuns.Select(j => j.Id).ToListAsync();
        remaining.ShouldNotContain(unreferenced.Id);
        remaining.ShouldContain(referencedByStrike.Id);
        remaining.ShouldContain(referencedByEvent.Id);
        remaining.ShouldContain(referencedByManualEvent.Id);
        remaining.ShouldContain(recent.Id);
        remaining.ShouldContain(incomplete.Id);
    }

    [Fact]
    public async Task CleanupStrikesAsync_DeletesStrikesOfItemsOutsideTheInactivityWindow()
    {
        JobRun run = new() { Id = Guid.NewGuid(), Type = JobType.QueueCleaner, StartedAt = Now.AddDays(-2) };
        _context.JobRuns.Add(run);

        DownloadItem inactive = new() { DownloadId = "inactive", Title = "inactive" };
        DownloadItem active = new() { DownloadId = "active", Title = "active" };
        _context.DownloadItems.AddRange(inactive, active);

        // The inactive item's most recent strike predates the window, so all of its strikes go.
        _context.Strikes.Add(new Strike
        {
            DownloadItemId = inactive.Id,
            JobRunId = run.Id,
            Type = StrikeType.Stalled,
            CreatedAt = Now.AddHours(-48),
        });
        _context.Strikes.Add(new Strike
        {
            DownloadItemId = inactive.Id,
            JobRunId = run.Id,
            Type = StrikeType.Stalled,
            CreatedAt = Now.AddHours(-30),
        });
        // The active item keeps every strike, including the old one, because it was struck again recently.
        _context.Strikes.Add(new Strike
        {
            DownloadItemId = active.Id,
            JobRunId = run.Id,
            Type = StrikeType.Stalled,
            CreatedAt = Now.AddHours(-48),
        });
        _context.Strikes.Add(new Strike
        {
            DownloadItemId = active.Id,
            JobRunId = run.Id,
            Type = StrikeType.Stalled,
            CreatedAt = Now.AddHours(-1),
        });
        await _context.SaveChangesAsync();

        await _service.CleanupStrikesAsync(_context, inactivityWindowHours: 24);

        List<Guid> remainingStrikes = await _context.Strikes.Select(s => s.DownloadItemId).ToListAsync();
        remainingStrikes.ShouldAllBe(id => id == active.Id);
        remainingStrikes.Count.ShouldBe(2);

        List<string> remainingItems = await _context.DownloadItems.Select(d => d.DownloadId).ToListAsync();
        remainingItems.ShouldBe(["active"]);
    }

    [Fact]
    public async Task PerformCleanupAsync_PrunesTransientRowsBeyondTheRetentionWindow()
    {
        DateTimeOffset old = Now.AddDays(-40);
        DateTimeOffset recent = Now.AddDays(-5);

        JobRun staleRun = new() { Id = Guid.NewGuid(), Type = JobType.QueueCleaner, StartedAt = old, CompletedAt = old };
        JobRun recentRun = new() { Id = Guid.NewGuid(), Type = JobType.QueueCleaner, StartedAt = recent, CompletedAt = recent };
        _context.JobRuns.AddRange(staleRun, recentRun);

        _context.ManualEvents.Add(new ManualEvent
        {
            Type = ManualEventType.RecurringDownload,
            Message = "stale",
            Severity = EventSeverity.Warning,
            Timestamp = old,
            IsResolved = true,
            ResolvedAt = old,
        });
        _context.ManualEvents.Add(new ManualEvent
        {
            Type = ManualEventType.RecurringDownload,
            Message = "recent",
            Severity = EventSeverity.Warning,
            Timestamp = old,
            IsResolved = true,
            ResolvedAt = recent,
        });
        await _context.SaveChangesAsync();

        await _service.PerformCleanupAsync();

        List<string> manualEvents = await _context.ManualEvents.Select(e => e.Message).ToListAsync();
        manualEvents.ShouldBe(["recent"]);

        List<Guid> jobRuns = await _context.JobRuns.Select(j => j.Id).ToListAsync();
        jobRuns.ShouldNotContain(staleRun.Id);
        jobRuns.ShouldContain(recentRun.Id);
    }
}
