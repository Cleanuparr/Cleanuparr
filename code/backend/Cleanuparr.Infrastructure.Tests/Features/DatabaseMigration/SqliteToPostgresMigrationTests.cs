using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DatabaseMigration;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Providers;
using Cleanuparr.Persistence.Models.Auth;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Events;
using Cleanuparr.Shared.Configuration;
using Cleanuparr.Shared.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DatabaseMigration;

[Collection("SeedParity")]
public class SqliteToPostgresMigrationTests
{
    private const string EventsMigrationBeforeCustomFormatScoreEntries = "20260706084236_EventStreamRework";

    [SkippableFact]
    public async Task RunAsync_copies_all_rows_preserves_keys_and_enforces_force_guard()
    {
        PostgreSqlContainer postgresContainer = await StartPostgresOrSkipAsync();

        string tempConfigDir = Path.Combine(Path.GetTempPath(), $"cleanuparr-migrator-{Guid.NewGuid():N}");
        string previousConfigPath = ConfigurationPathProvider.GetConfigPath();

        Guid arrConfigId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        Guid appEventId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        Guid userId = Guid.Parse("44444444-4444-4444-4444-444444444444");

        try
        {
            ConfigurationPathProvider.SetConfigPath(tempConfigDir);

            int arrConfigCount = await SeedSqliteSourceAsync(arrConfigId, appEventId, userId);

            InitializeDatabaseConfig(postgresContainer);

            SqliteToPostgresMigrator migrator = new();

            MigrationResult result = await migrator.RunAsync(force: false, null, CancellationToken.None);
            result.Success.ShouldBeTrue(result.Error);
            result.TableCounts["arr_configs"].ShouldBe(arrConfigCount);
            result.TableCounts["events"].ShouldBe(1);
            result.TableCounts["users"].ShouldBe(1);

            await AssertKeysPreservedAsync(postgresContainer, arrConfigId, appEventId, userId);

            await AssertAppSeesMigrationsAppliedAsync();

            MigrationResult second = await migrator.RunAsync(force: false, null, CancellationToken.None);
            second.Success.ShouldBeFalse();

            MigrationResult forced = await migrator.RunAsync(force: true, null, CancellationToken.None);
            forced.Success.ShouldBeTrue(forced.Error);
            forced.TableCounts["arr_configs"].ShouldBe(arrConfigCount);
        }
        finally
        {
            DatabaseConfigProvider.Initialize(new ConfigurationBuilder().Build());
            ConfigurationPathProvider.SetConfigPath(previousConfigPath);
            await postgresContainer.DisposeAsync();

            if (Directory.Exists(tempConfigDir))
            {
                Directory.Delete(tempConfigDir, recursive: true);
            }
        }
    }

    [SkippableFact]
    public async Task RunAsync_upgrades_a_stale_sqlite_source_before_copying()
    {
        PostgreSqlContainer postgresContainer = await StartPostgresOrSkipAsync();

        string tempConfigDir = Path.Combine(Path.GetTempPath(), $"cleanuparr-migrator-stale-{Guid.NewGuid():N}");
        string previousConfigPath = ConfigurationPathProvider.GetConfigPath();

        Guid arrConfigId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        Guid userId = Guid.Parse("66666666-6666-6666-6666-666666666666");

        try
        {
            ConfigurationPathProvider.SetConfigPath(tempConfigDir);

            int arrConfigCount = await SeedStaleSqliteSourceAsync(arrConfigId, userId);

            InitializeDatabaseConfig(postgresContainer);

            SqliteToPostgresMigrator migrator = new();
            MigrationResult result = await migrator.RunAsync(force: false, null, CancellationToken.None);

            result.Success.ShouldBeTrue(result.Error);
            result.TableCounts["arr_configs"].ShouldBe(arrConfigCount);
            result.TableCounts.ShouldContainKey("custom_format_score_entries");
        }
        finally
        {
            DatabaseConfigProvider.Initialize(new ConfigurationBuilder().Build());
            ConfigurationPathProvider.SetConfigPath(previousConfigPath);
            await postgresContainer.DisposeAsync();

            if (Directory.Exists(tempConfigDir))
            {
                Directory.Delete(tempConfigDir, recursive: true);
            }
        }
    }

    private static async Task<PostgreSqlContainer> StartPostgresOrSkipAsync()
    {
        try
        {
            PostgreSqlContainer container = new PostgreSqlBuilder()
                .WithImage("postgres:17")
                .Build();

            await container.StartAsync();
            return container;
        }
        catch (Exception exception)
        {
            throw new SkipException($"Docker is unavailable, skipping migrator test: {exception.Message}");
        }
    }

    private static async Task<int> SeedStaleSqliteSourceAsync(Guid arrConfigId, Guid userId)
    {
        await using DataContext data = DataContext.CreateStaticInstance();
        await data.Database.MigrateAsync();
        data.ArrConfigs.Add(new ArrConfig { Id = arrConfigId, Type = InstanceType.Sonarr });
        await data.SaveChangesAsync();
        int arrConfigCount = await data.ArrConfigs.CountAsync();

        await using UsersContext users = UsersContext.CreateStaticInstance();
        await users.Database.MigrateAsync();
        users.Users.Add(new User
        {
            Id = userId,
            Username = "stale-user",
            PasswordHash = "hash",
            TotpSecret = "secret",
            ApiKey = "api-key",
        });
        await users.SaveChangesAsync();

        await using EventsContext events = EventsContext.CreateStaticInstance();
        await events.Database.MigrateAsync(EventsMigrationBeforeCustomFormatScoreEntries);

        return arrConfigCount;
    }

    private static async Task<int> SeedSqliteSourceAsync(Guid arrConfigId, Guid appEventId, Guid userId)
    {
        await using DataContext data = DataContext.CreateStaticInstance();
        await data.Database.MigrateAsync();
        data.ArrConfigs.Add(new ArrConfig { Id = arrConfigId, Type = InstanceType.Sonarr });
        await data.SaveChangesAsync();
        int arrConfigCount = await data.ArrConfigs.CountAsync();

        await using EventsContext events = EventsContext.CreateStaticInstance();
        await events.Database.MigrateAsync();
        events.Events.Add(new AppEvent
        {
            Id = appEventId,
            EventType = EventType.QueueItemDeleted,
            Message = "seed event",
            Severity = EventSeverity.Information,
        });
        await events.SaveChangesAsync();

        await using UsersContext users = UsersContext.CreateStaticInstance();
        await users.Database.MigrateAsync();
        users.Users.Add(new User
        {
            Id = userId,
            Username = "seed-user",
            PasswordHash = "hash",
            TotpSecret = "secret",
            ApiKey = "api-key",
        });
        await users.SaveChangesAsync();

        return arrConfigCount;
    }

    private static async Task AssertAppSeesMigrationsAppliedAsync()
    {
        PostgresDatabaseProvider provider = new();

        await using DataContext data = BuildAppContext<DataContext>(provider, DbContextKind.Data);
        (await data.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();

        await using EventsContext events = BuildAppContext<EventsContext>(provider, DbContextKind.Events);
        (await events.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();

        await using UsersContext users = BuildAppContext<UsersContext>(provider, DbContextKind.Users);
        (await users.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
    }

    private static TContext BuildAppContext<TContext>(PostgresDatabaseProvider provider, DbContextKind kind)
        where TContext : DbContext
    {
        DbContextOptionsBuilder<TContext> builder = new();
        provider.ConfigureContext(builder, kind);
        return (TContext)Activator.CreateInstance(typeof(TContext), builder.Options, provider)!;
    }

    private static void InitializeDatabaseConfig(PostgreSqlContainer postgresContainer)
    {
        NpgsqlConnectionStringBuilder parsed = new(postgresContainer.GetConnectionString());

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ConfigurationKeys.PostgresHost] = parsed.Host,
                [ConfigurationKeys.PostgresPort] = parsed.Port.ToString(),
                [ConfigurationKeys.PostgresUser] = parsed.Username,
                [ConfigurationKeys.PostgresPassword] = parsed.Password,
                [ConfigurationKeys.PostgresDatabase] = parsed.Database,
            })
            .Build();

        DatabaseConfigProvider.Initialize(configuration);
    }

    private static async Task AssertKeysPreservedAsync(PostgreSqlContainer postgresContainer, Guid arrConfigId, Guid appEventId, Guid userId)
    {
        await using NpgsqlConnection connection = new(postgresContainer.GetConnectionString());
        await connection.OpenAsync();

        await using NpgsqlCommand arrCommand = new("SELECT COUNT(*) FROM data.arr_configs WHERE id = @id", connection);
        arrCommand.Parameters.AddWithValue("id", arrConfigId);
        long arrMatches = (long)(await arrCommand.ExecuteScalarAsync())!;
        arrMatches.ShouldBe(1L);

        await using NpgsqlCommand eventCommand = new("SELECT COUNT(*) FROM events.events WHERE id = @id", connection);
        eventCommand.Parameters.AddWithValue("id", appEventId);
        long eventMatches = (long)(await eventCommand.ExecuteScalarAsync())!;
        eventMatches.ShouldBe(1L);

        await using NpgsqlCommand userCommand = new("SELECT COUNT(*) FROM users.users WHERE id = @id", connection);
        userCommand.Parameters.AddWithValue("id", userId);
        long userMatches = (long)(await userCommand.ExecuteScalarAsync())!;
        userMatches.ShouldBe(1L);
    }
}
