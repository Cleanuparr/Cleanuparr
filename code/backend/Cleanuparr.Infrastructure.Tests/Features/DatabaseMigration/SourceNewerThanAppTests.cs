using Cleanuparr.Infrastructure.Features.DatabaseMigration;
using Cleanuparr.Persistence;
using Cleanuparr.Shared.Configuration;
using Cleanuparr.Shared.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DatabaseMigration;

[Collection("SeedParity")]
public class SourceNewerThanAppTests
{
    private const string UnknownMigration = "29990101000000_FromTheFuture";

    [Fact]
    public async Task RunAsync_refuses_a_source_holding_migrations_this_build_does_not_ship()
    {
        string tempConfigDir = Path.Combine(Path.GetTempPath(), $"cleanuparr-newer-source-{Guid.NewGuid():N}");
        string previousConfigPath = ConfigurationPathProvider.GetConfigPath();

        try
        {
            ConfigurationPathProvider.SetConfigPath(tempConfigDir);
            await SeedSourceWithUnknownMigrationAsync();
            InitializeUnreachablePostgresConfig();

            SqliteToPostgresMigrator migrator = new();

            // force skips the target probe, so nothing here needs a live PostgreSQL.
            MigrationResult result = await migrator.RunAsync(force: true, null, CancellationToken.None);

            result.Success.ShouldBeFalse();
            result.Error.ShouldNotBeNull();
            result.Error!.ShouldContain(UnknownMigration);
            result.Error.ShouldContain("newer version of Cleanuparr");
        }
        finally
        {
            ConfigurationPathProvider.SetConfigPath(previousConfigPath);
            if (Directory.Exists(tempConfigDir))
            {
                Directory.Delete(tempConfigDir, recursive: true);
            }
        }
    }

    private static async Task SeedSourceWithUnknownMigrationAsync()
    {
        await using DataContext data = DataContext.CreateStaticInstance();
        await data.Database.MigrateAsync();

        string historyTable = await ReadHistoryTableNameAsync(data);
        await data.Database.ExecuteSqlRawAsync(
            $"INSERT INTO \"{historyTable}\" (migration_id, product_version) VALUES ({{0}}, {{1}})",
            UnknownMigration,
            "99.0.0");
    }

    private static async Task<string> ReadHistoryTableNameAsync(DataContext context)
    {
        List<string> tables = await context.Database
            .SqlQueryRaw<string>("SELECT name FROM sqlite_master WHERE type = 'table' AND lower(name) LIKE '%migrations%history%'")
            .ToListAsync();

        return tables.ShouldHaveSingleItem();
    }

    private static void InitializeUnreachablePostgresConfig()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ConfigurationKeys.PostgresHost] = "127.0.0.1",
                [ConfigurationKeys.PostgresPort] = "1",
                [ConfigurationKeys.PostgresUser] = "unused",
                [ConfigurationKeys.PostgresPassword] = "unused",
                [ConfigurationKeys.PostgresDatabase] = "unused",
            })
            .Build();

        DatabaseConfigProvider.Initialize(configuration);
    }
}
