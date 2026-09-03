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
/// The *arr apps report an uppercase download id while the download clients report a lowercase hash.
/// These strike the same torrent through both casings against a real migrated database.
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
    public async Task Same_torrent_struck_through_both_casings_splits_into_two_download_items()
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

        // Today one torrent yields two rows with its strikes split across them.
        // Once the casing is normalised this must become a single row holding both strikes.
        items.Count.ShouldBe(2);
        items[0].DownloadId.ShouldBe(UppercaseHash);
        items[1].DownloadId.ShouldBe(LowercaseHash);
        items[0].Strikes.Count.ShouldBe(1);
        items[1].Strikes.Count.ShouldBe(1);
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
