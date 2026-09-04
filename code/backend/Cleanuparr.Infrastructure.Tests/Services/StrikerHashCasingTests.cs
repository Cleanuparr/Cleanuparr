using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events.Interfaces;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.State;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Services;

/// <summary>
/// The *arr apps report an uppercase download id.
/// The download clients report a lowercase hash.
/// Both casings are one torrent: striking through either lands on the same lowercase row.
/// Struck against a real migrated database, since the invariant is the stored column's.
/// </summary>
public sealed class StrikerHashCasingTests : IAsyncLifetime
{
    private const string UppercaseHash = "ABCDEF0123456789ABCDEF0123456789ABCDEF01";

    private const string LowercaseHash = "abcdef0123456789abcdef0123456789abcdef01";

    private readonly SqliteTestDatabase _events = SqliteTestDatabase.Create("striker-hash-casing");

    private readonly Guid _jobRunId = Guid.CreateVersion7();

    public async Task InitializeAsync()
    {
        await using EventsContext events = CreateContext();
        await events.Database.MigrateAsync();

        events.JobRuns.Add(new JobRun { Id = _jobRunId, Type = JobType.QueueCleaner });
        await events.SaveChangesAsync();

        Striker.RecurringHashes.Clear();
    }

    public Task DisposeAsync()
    {
        _events.Dispose();
        Striker.RecurringHashes.Clear();

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Same_torrent_struck_through_both_casings_shares_one_download_item()
    {
        // The context is async local, so the strikes must claim the job run from inside the test.
        ContextProvider.SetJobRunId(_jobRunId);

        await using (EventsContext arrContext = CreateContext())
        {
            await CreateStriker(arrContext).StrikeAndCheckLimit(UppercaseHash, "Test Item", 3, StrikeType.Stalled);
        }

        await using (EventsContext clientContext = CreateContext())
        {
            await CreateStriker(clientContext).StrikeAndCheckLimit(LowercaseHash, "Test Item", 3, StrikeType.Stalled);
        }

        await using EventsContext assertions = CreateContext();
        List<DownloadItem> items = await assertions.DownloadItems
            .AsNoTracking()
            .Include(d => d.Strikes)
            .OrderBy(d => d.DownloadId)
            .ToListAsync();

        items.Count.ShouldBe(1);
        items[0].DownloadId.ShouldBe(LowercaseHash);
        items[0].Strikes.Count.ShouldBe(2);
    }

    private static Striker CreateStriker(EventsContext context)
    {
        IDryRunInterceptor dryRunInterceptor = Substitute.For<IDryRunInterceptor>();
        dryRunInterceptor.IsDryRunEnabled().Returns(false);

        return new Striker(
            Substitute.For<ILogger<Striker>>(),
            context,
            Substitute.For<IEventPublisher>(),
            dryRunInterceptor);
    }

    private EventsContext CreateContext() => _events.CreateContext<EventsContext>();
}
