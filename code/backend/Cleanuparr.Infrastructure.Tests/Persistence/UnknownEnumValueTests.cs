using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Cleanuparr.Persistence.Models.Configuration.General;
using Cleanuparr.Persistence.Models.Configuration.MalwareBlocker;
using Cleanuparr.Persistence.Models.State;
using Cleanuparr.Persistence.Providers;
using Microsoft.EntityFrameworkCore;
using Serilog.Events;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Persistence;

/// <summary>
/// A rollback leaves enum values the build does not know.
/// These cover what each kind of column does with one.
/// </summary>
public sealed class UnknownEnumValueTests : IAsyncLifetime
{
    private readonly SqliteTestDatabase _data = SqliteTestDatabase.Create("unknown-enum-data");

    private readonly SqliteTestDatabase _events = SqliteTestDatabase.Create("unknown-enum-events");

    public async Task InitializeAsync()
    {
        await using DataContext data = CreateDataContext();
        await data.Database.MigrateAsync();

        await using EventsContext events = CreateEventsContext();
        await events.Database.MigrateAsync();
    }

    public Task DisposeAsync()
    {
        _data.Dispose();
        _events.Dispose();

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Nullable_column_reads_an_unknown_value_as_null()
    {
        Guid id = Guid.CreateVersion7();

        await using (EventsContext seed = CreateEventsContext())
        {
            seed.JobRuns.Add(new JobRun { Id = id, Type = JobType.QueueCleaner, Status = JobRunStatus.Completed });
            await seed.SaveChangesAsync();

            await seed.Database.ExecuteSqlRawAsync(
                "UPDATE job_runs SET status = 'fromthefuture' WHERE id = {0}",
                id);
        }

        await using EventsContext context = CreateEventsContext();
        JobRun? loaded = await context.JobRuns.SingleOrDefaultAsync(x => x.Id == id);

        loaded.ShouldNotBeNull();
        loaded!.Status.ShouldBeNull();
        loaded.Type.ShouldBe(JobType.QueueCleaner);
    }

    [Fact]
    public async Task Identity_column_reads_an_unknown_value_as_the_sentinel()
    {
        await PoisonLidarrAsync();

        await using DataContext context = CreateDataContext();

        // This shape threw before the change.
        List<ArrConfig> all = await context.ArrConfigs.Include(x => x.Instances).ToListAsync();

        all.Count(c => c.Type == InstanceType.Unknown).ShouldBe(1);
        all.ShouldNotContain(c => c.Type == InstanceType.Lidarr);
    }

    [Fact]
    public async Task Unknown_never_reads_as_the_first_member()
    {
        await PoisonLidarrAsync();

        await using DataContext context = CreateDataContext();
        List<ArrConfig> all = await context.ArrConfigs.ToListAsync();

        // Falling back to ordinal 0 would hand Sonarr's client a Lidarr instance.
        all.Count(c => c.Type == InstanceType.Sonarr).ShouldBe(1);
    }

    [Fact]
    public async Task Naming_a_type_excludes_the_unknown_row_in_sql()
    {
        await PoisonLidarrAsync();

        await using DataContext context = CreateDataContext();

        ArrConfig sonarr = await context.ArrConfigs.FirstAsync(c => c.Type == InstanceType.Sonarr);

        sonarr.Type.ShouldBe(InstanceType.Sonarr);
    }

    [Fact]
    public async Task Comparing_against_the_sentinel_in_sql_is_refused()
    {
        await using DataContext context = CreateDataContext();

        // The column holds the original text, so this comparison matches nothing.
        // Better to throw than to return an empty set.
        await Should.ThrowAsync<InvalidOperationException>(
            () => context.ArrConfigs.CountAsync(c => c.Type == InstanceType.Unknown));
    }

    [Fact]
    public async Task Settings_column_falls_back_to_its_declared_default()
    {
        await using (DataContext seed = CreateDataContext())
        {
            await seed.Database.ExecuteSqlRawAsync(
                "UPDATE general_configs SET log_level = 'fromthefuture', http_certificate_validation = 'fromthefuture'");
        }

        await using DataContext context = CreateDataContext();
        GeneralConfig config = await context.GeneralConfigs.FirstAsync();

        // Ordinal 0 is Verbose, which would fill the disk.
        config.Log.Level.ShouldBe(LogEventLevel.Information);
        config.HttpCertificateValidation.ShouldBe(CertificateValidationType.Enabled);
    }

    [Fact]
    public async Task Settings_row_is_never_hidden_by_an_unknown_value()
    {
        await using (DataContext seed = CreateDataContext())
        {
            await seed.Database.ExecuteSqlRawAsync(
                "UPDATE content_blocker_configs SET sonarr_blocklist_type = 'fromthefuture'");
        }

        await using DataContext context = CreateDataContext();
        ContentBlockerConfig config = await context.ContentBlockerConfigs.FirstAsync();

        // Dropping the row would leave the app with no malware settings.
        config.Sonarr.BlocklistType.ShouldBe(BlocklistType.Blacklist);
    }

    [Fact]
    public async Task Seeding_rule_action_reads_an_unknown_value_as_the_sentinel()
    {
        Guid ruleId = Guid.CreateVersion7();

        await using (DataContext seed = CreateDataContext())
        {
            DownloadClientConfig client = new()
            {
                Id = Guid.CreateVersion7(),
                Name = "Test qBittorrent",
                TypeName = DownloadClientTypeName.qBittorrent,
                Type = DownloadClientType.Torrent,
                Enabled = true,
                Host = new Uri("http://localhost:8080")
            };

            seed.DownloadClients.Add(client);
            seed.QBitSeedingRules.Add(new QBitSeedingRule
            {
                Id = ruleId,
                Name = "completed",
                Categories = ["completed"],
                MaxRatio = 1,
                DownloadClientConfigId = client.Id
            });
            await seed.SaveChangesAsync();

            await seed.Database.ExecuteSqlRawAsync(
                "UPDATE q_bit_seeding_rules SET action = 'fromthefuture' WHERE id = {0}",
                ruleId);
        }

        await using DataContext context = CreateDataContext();
        QBitSeedingRule rule = await context.QBitSeedingRules.SingleAsync(x => x.Id == ruleId);

        // Falling back to ordinal 0 would delete downloads a newer version meant to stop.
        rule.Action.ShouldBe(SeedingRuleAction.Unknown);
    }

    private async Task PoisonLidarrAsync()
    {
        await using DataContext seed = CreateDataContext();
        await seed.Database.ExecuteSqlRawAsync(
            "UPDATE arr_configs SET type = 'fromthefuture' WHERE type = 'lidarr'");
    }

    private DataContext CreateDataContext() => _data.CreateContext<DataContext>();

    private EventsContext CreateEventsContext() => _events.CreateContext<EventsContext>();
}
