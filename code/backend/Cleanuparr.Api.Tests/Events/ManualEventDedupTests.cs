using Cleanuparr.Api.Tests.Features.Seeker.TestHelpers;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Events;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Cleanuparr.Api.Tests.Events;

/// <summary>
/// Exercises the manual-event partial unique index against a real SQLite context configured with the
/// production naming conventions. The EF Core InMemory provider ignores unique indexes, so the guarantee
/// that <see cref="Cleanuparr.Infrastructure.Events.EventPublisher.PublishManualAsync"/> relies on to
/// dedup racing publishers can only be verified here.
/// </summary>
public class ManualEventDedupTests : IDisposable
{
    private readonly EventsContext _context;

    public ManualEventDedupTests()
    {
        _context = SeekerTestDataFactory.CreateEventsContext();
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private static ManualEvent NewEvent(string hash, bool isResolved) => new()
    {
        Type = ManualEventType.RecurringDownload,
        Message = "m",
        Severity = EventSeverity.Warning,
        ItemHash = hash,
        IsResolved = isResolved,
    };

    [Fact]
    public async Task TwoUnresolvedSameTypeAndHash_ViolatesUniqueIndex_WithSqliteConstraintError()
    {
        _context.ManualEvents.Add(NewEvent("abc123", isResolved: false));
        _context.ManualEvents.Add(NewEvent("abc123", isResolved: false));

        // The exception must surface as SQLITE_CONSTRAINT (19) — the exact code PublishManualAsync's
        // catch filters on to treat the loser of a race as deduped.
        DbUpdateException ex = await Should.ThrowAsync<DbUpdateException>(() => _context.SaveChangesAsync());
        SqliteException sqliteEx = ex.InnerException.ShouldBeOfType<SqliteException>();
        sqliteEx.SqliteErrorCode.ShouldBe(19);
    }

    [Fact]
    public async Task ResolvedDuplicate_IsExemptFromUniqueIndex()
    {
        _context.ManualEvents.Add(NewEvent("abc123", isResolved: false));
        await _context.SaveChangesAsync();

        // The index is filtered on "is_resolved = 0", so a resolved row with the same type/hash is allowed.
        _context.ManualEvents.Add(NewEvent("abc123", isResolved: true));
        await Should.NotThrowAsync(() => _context.SaveChangesAsync());

        (await _context.ManualEvents.CountAsync()).ShouldBe(2);
    }
}
