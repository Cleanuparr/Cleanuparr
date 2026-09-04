using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Events;
using Cleanuparr.Persistence.Models.State;
using Cleanuparr.Persistence.Providers;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Persistence;

/// <summary>
/// NormalizeDownloadHashCasing merges the case-variant rows an older build left.
/// Every strike survives, and the unique index still bites afterwards.
/// </summary>
[Collection("SeedParity")]
public class DownloadHashCasingMigrationTests
{
    private const string SqliteMigrationBeforeHashCasing = "20260710110253_MoveSeekerStateToEvents";

    private const string PostgresMigrationBeforeHashCasing = "20260710201654_InitialPostgres";

    private const string SqliteIndexProbe =
        "SELECT name AS \"Value\" FROM sqlite_master WHERE type = 'index' AND name = 'ix_download_items_download_id'";

    private const string PostgresIndexProbe =
        "SELECT indexname AS \"Value\" FROM pg_indexes WHERE schemaname = 'events' AND indexname = 'ix_download_items_download_id'";

    private const string UpperHash = "ABCDEF0123456789ABCDEF0123456789ABCDEF01";

    private const string LowerHash = "abcdef0123456789abcdef0123456789abcdef01";

    private const string MixedHash = "AbCdEf0123456789aBcDeF0123456789AbCdEf01";

    private const string ControlHash = "99887766554433221100aabbccddeeff00112233";

    private const string SurvivorTitle = "Torrent.Survivor.Title";

    private static readonly Guid JobRunId = new("11111111-1111-1111-1111-111111111111");

    // Ordered so the survivor is the lowest id under both TEXT and uuid comparison.
    private static readonly Guid SurvivorId = new("00000000-0000-0000-0000-000000000001");

    private static readonly Guid SecondId = new("00000000-0000-0000-0000-000000000002");

    private static readonly Guid ThirdId = new("00000000-0000-0000-0000-000000000003");

    private static readonly Guid ControlId = new("00000000-0000-0000-0000-000000000009");

    [Fact]
    public async Task Case_variant_download_items_merge_on_sqlite()
    {
        using SqliteTestDatabase sqlite = SqliteTestDatabase.Create("hash-casing-migration");

        await RunAsync(
            sqlite.CreateContext<EventsContext>,
            sqlite.Provider,
            SqliteMigrationBeforeHashCasing,
            SqliteIndexProbe);
    }

    [SkippableFact]
    public async Task Case_variant_download_items_merge_on_postgres()
    {
        PostgreSqlContainer postgresContainer;

        try
        {
            postgresContainer = new PostgreSqlBuilder()
                .WithImage("postgres:17")
                .Build();

            await postgresContainer.StartAsync();
        }
        catch (Exception exception)
        {
            throw new SkipException($"Docker is unavailable, skipping hash casing migration test: {exception.Message}");
        }

        try
        {
            PostgresDatabaseProvider provider = new();
            string connectionString = postgresContainer.GetConnectionString();

            await RunAsync(
                () => CreatePostgresContext(connectionString, provider),
                provider,
                PostgresMigrationBeforeHashCasing,
                PostgresIndexProbe);
        }
        finally
        {
            await postgresContainer.DisposeAsync();
        }
    }

    private static EventsContext CreatePostgresContext(string connectionString, PostgresDatabaseProvider provider)
    {
        DbContextOptionsBuilder<EventsContext> builder = new();
        builder
            .UseNpgsql(connectionString, options => options.MigrationsAssembly(PostgresDatabaseProvider.MigrationsAssembly))
            .UseLowerCaseNamingConvention()
            .UseSnakeCaseNamingConvention();

        return new EventsContext(builder.Options, provider);
    }

    private static async Task RunAsync(
        Func<EventsContext> contextFactory,
        IDatabaseProvider provider,
        string previousMigration,
        string indexProbe)
    {
        await using (EventsContext seed = contextFactory())
        {
            await seed.Database.MigrateAsync(previousMigration);
            await SeedAsync(seed, provider);
        }

        await using (EventsContext migrate = contextFactory())
        {
            await migrate.Database.MigrateAsync();
        }

        await using (EventsContext assert = contextFactory())
        {
            await AssertMergedAsync(assert, indexProbe);
        }
    }

    /// <summary>
    /// Plants one torrent as three rows differing only in casing, plus a single-cased control.
    /// The casing goes in through raw SQL, because the converter would lowercase an EF write.
    /// </summary>
    private static async Task SeedAsync(EventsContext context, IDatabaseProvider provider)
    {
        context.JobRuns.Add(new JobRun { Id = JobRunId, Type = JobType.QueueCleaner });

        AddDownloadItem(context, SurvivorId, "placeholder-1", SurvivorTitle);
        AddDownloadItem(context, SecondId, "placeholder-2", "Torrent.Loser.Two", isRemoved: true);
        AddDownloadItem(context, ThirdId, "placeholder-3", "Torrent.Loser.Three", isMarkedForRemoval: true, isReturning: true);
        AddDownloadItem(context, ControlId, "placeholder-4", "Torrent.Control");

        AddStrikes(context, SurvivorId, StrikeType.Stalled);
        AddStrikes(context, SecondId, StrikeType.SlowSpeed, StrikeType.SlowTime);
        AddStrikes(context, ThirdId, StrikeType.Stalled, StrikeType.FailedImport, StrikeType.DeadTorrent);
        AddStrikes(context, ControlId, StrikeType.Stalled, StrikeType.SlowSpeed);

        context.Events.Add(new AppEvent
        {
            EventType = EventType.QueueItemDeleted,
            Message = "hash casing",
            Severity = EventSeverity.Information,
            ItemHash = "placeholder-hash",
        });

        await context.SaveChangesAsync();

        string downloadItems = provider.QualifyTable("download_items", DbContextKind.Events);
        string events = provider.QualifyTable("events", DbContextKind.Events);

        await StoreHashAsync(context, downloadItems, SurvivorId, UpperHash);
        await StoreHashAsync(context, downloadItems, SecondId, LowerHash);
        await StoreHashAsync(context, downloadItems, ThirdId, MixedHash);
        await StoreHashAsync(context, downloadItems, ControlId, ControlHash);

        await context.Database.ExecuteSqlRawAsync(
            $"UPDATE {events} SET item_hash = {{0}} WHERE item_hash = 'placeholder-hash'",
            UpperHash);
    }

    private static void AddDownloadItem(
        EventsContext context,
        Guid id,
        string placeholderHash,
        string title,
        bool isMarkedForRemoval = false,
        bool isRemoved = false,
        bool isReturning = false)
    {
        context.DownloadItems.Add(new DownloadItem
        {
            Id = id,
            DownloadId = placeholderHash,
            Title = title,
            IsMarkedForRemoval = isMarkedForRemoval,
            IsRemoved = isRemoved,
            IsReturning = isReturning,
        });
    }

    private static void AddStrikes(EventsContext context, Guid downloadItemId, params StrikeType[] types)
    {
        foreach (StrikeType type in types)
        {
            context.Strikes.Add(new Strike
            {
                DownloadItemId = downloadItemId,
                JobRunId = JobRunId,
                Type = type,
            });
        }
    }

    private static Task StoreHashAsync(EventsContext context, string table, Guid id, string hash) =>
        context.Database.ExecuteSqlRawAsync($"UPDATE {table} SET download_id = {{0}} WHERE id = {{1}}", hash, id);

    private static async Task AssertMergedAsync(EventsContext context, string indexProbe)
    {
        List<DownloadItem> items = await context.DownloadItems
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .ToListAsync();

        items.Count.ShouldBe(2);

        DownloadItem survivor = items.Single(item => item.Id == SurvivorId);
        survivor.DownloadId.ShouldBe(LowerHash);
        survivor.Title.ShouldBe(SurvivorTitle);
        survivor.IsRemoved.ShouldBeTrue();
        survivor.IsMarkedForRemoval.ShouldBeTrue();
        survivor.IsReturning.ShouldBeTrue();

        DownloadItem control = items.Single(item => item.Id == ControlId);
        control.DownloadId.ShouldBe(ControlHash);
        control.Title.ShouldBe("Torrent.Control");
        control.IsRemoved.ShouldBeFalse();
        control.IsMarkedForRemoval.ShouldBeFalse();
        control.IsReturning.ShouldBeFalse();

        List<Strike> strikes = await context.Strikes.AsNoTracking().ToListAsync();
        strikes.Count.ShouldBe(8);
        strikes.Count(strike => strike.DownloadItemId == SurvivorId).ShouldBe(6);
        strikes.Count(strike => strike.DownloadItemId == ControlId).ShouldBe(2);

        // The merge keeps both Stalled strikes from the same job run.
        strikes
            .Count(strike => strike.DownloadItemId == SurvivorId
                && strike.Type == StrikeType.Stalled
                && strike.JobRunId == JobRunId)
            .ShouldBe(2);

        AppEvent appEvent = await context.Events.AsNoTracking().SingleAsync();
        appEvent.ItemHash.ShouldBe(LowerHash);

        List<string> indexes = await context.Database.SqlQueryRaw<string>(indexProbe).ToListAsync();
        indexes.ShouldBe(new[] { "ix_download_items_download_id" });

        // Any casing now lands on the surviving row, so a second insert collides.
        context.DownloadItems.Add(new DownloadItem { DownloadId = UpperHash, Title = "Torrent.Duplicate" });
        await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
}
