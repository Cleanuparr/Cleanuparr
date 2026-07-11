using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.BlacklistSync;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Cleanuparr.Persistence.Models.Configuration.General;
using Cleanuparr.Persistence.Models.Configuration.MalwareBlocker;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Cleanuparr.Persistence.Models.Configuration.Seeker;
using Cleanuparr.Persistence.Providers;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Persistence;

[CollectionDefinition("SeedParity", DisableParallelization = true)]
public class SeedParityCollection;

[Collection("SeedParity")]
public class SeedParityTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [SkippableFact]
    public async Task Fresh_migrated_postgres_and_sqlite_have_matching_seed_data()
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
            throw new SkipException($"Docker is unavailable, skipping seed parity test: {exception.Message}");
        }

        string sqlitePath = Path.Combine(Path.GetTempPath(), $"cleanuparr-seed-parity-{Guid.NewGuid():N}.db");

        try
        {
            SeedSnapshot sqliteSnapshot = await LoadSqliteSeedAsync(sqlitePath);
            SeedSnapshot postgresSnapshot = await LoadPostgresSeedAsync(postgresContainer);

            AssertSingletonMatches("general_configs", sqliteSnapshot.GeneralConfig, postgresSnapshot.GeneralConfig);
            AssertSingletonMatches("queue_cleaner_configs", sqliteSnapshot.QueueCleanerConfig, postgresSnapshot.QueueCleanerConfig);
            AssertSingletonMatches("content_blocker_configs", sqliteSnapshot.ContentBlockerConfig, postgresSnapshot.ContentBlockerConfig);
            AssertSingletonMatches("download_cleaner_configs", sqliteSnapshot.DownloadCleanerConfig, postgresSnapshot.DownloadCleanerConfig);
            AssertSingletonMatches("seeker_configs", sqliteSnapshot.SeekerConfig, postgresSnapshot.SeekerConfig);
            AssertSingletonMatches("blacklist_sync_configs", sqliteSnapshot.BlacklistSyncConfig, postgresSnapshot.BlacklistSyncConfig);

            AssertArrConfigsMatch(sqliteSnapshot.ArrConfigs, postgresSnapshot.ArrConfigs);
        }
        finally
        {
            DatabaseProviderFactory.SetOverrideForTesting(null);
            await postgresContainer.DisposeAsync();

            if (File.Exists(sqlitePath))
            {
                File.Delete(sqlitePath);
            }
        }
    }

    private static async Task<SeedSnapshot> LoadSqliteSeedAsync(string sqlitePath)
    {
        DbContextOptionsBuilder<DataContext> optionsBuilder = new();
        optionsBuilder
            .UseSqlite($"Data Source={sqlitePath}", options => options.MigrationsAssembly("Cleanuparr.Persistence.Sqlite"))
            .UseLowerCaseNamingConvention()
            .UseSnakeCaseNamingConvention();

        await using DataContext context = new(optionsBuilder.Options);
        await context.Database.MigrateAsync();

        return await LoadSnapshotAsync(context);
    }

    private static async Task<SeedSnapshot> LoadPostgresSeedAsync(PostgreSqlContainer postgresContainer)
    {
        DatabaseProviderFactory.SetOverrideForTesting(new PostgresDatabaseProvider());

        DbContextOptionsBuilder<DataContext> optionsBuilder = new();
        optionsBuilder
            .UseNpgsql(postgresContainer.GetConnectionString(), options => options.MigrationsAssembly("Cleanuparr.Persistence.Postgres"))
            .UseLowerCaseNamingConvention()
            .UseSnakeCaseNamingConvention();

        await using DataContext context = new(optionsBuilder.Options);
        await context.Database.MigrateAsync();

        return await LoadSnapshotAsync(context);
    }

    private static async Task<SeedSnapshot> LoadSnapshotAsync(DataContext context)
    {
        return new SeedSnapshot
        {
            GeneralConfig = await context.GeneralConfigs.AsNoTracking().SingleAsync(),
            QueueCleanerConfig = await context.QueueCleanerConfigs.AsNoTracking().SingleAsync(),
            ContentBlockerConfig = await context.ContentBlockerConfigs.AsNoTracking().SingleAsync(),
            DownloadCleanerConfig = await context.DownloadCleanerConfigs.AsNoTracking().SingleAsync(),
            SeekerConfig = await context.SeekerConfigs.AsNoTracking().SingleAsync(),
            BlacklistSyncConfig = await context.BlacklistSyncConfigs.AsNoTracking().SingleAsync(),
            ArrConfigs = await context.ArrConfigs.AsNoTracking().ToListAsync(),
        };
    }

    private static void AssertSingletonMatches<T>(string tableName, T sqliteEntity, T postgresEntity)
    {
        string sqliteJson = Serialize(sqliteEntity);
        string postgresJson = Serialize(postgresEntity);

        if (sqliteJson != postgresJson)
        {
            throw new Xunit.Sdk.XunitException(
                $"Seed data mismatch in table '{tableName}'.\nSQLite:   {sqliteJson}\nPostgres: {postgresJson}");
        }
    }

    private static void AssertArrConfigsMatch(List<ArrConfig> sqliteConfigs, List<ArrConfig> postgresConfigs)
    {
        Dictionary<InstanceType, ArrConfig> sqliteByType = sqliteConfigs.ToDictionary(c => c.Type);
        Dictionary<InstanceType, ArrConfig> postgresByType = postgresConfigs.ToDictionary(c => c.Type);

        if (!sqliteByType.Keys.OrderBy(t => t).SequenceEqual(postgresByType.Keys.OrderBy(t => t)))
        {
            throw new Xunit.Sdk.XunitException(
                $"Seed data mismatch in table 'arr_configs': type sets differ.\nSQLite types:   {string.Join(", ", sqliteByType.Keys.OrderBy(t => t))}\nPostgres types: {string.Join(", ", postgresByType.Keys.OrderBy(t => t))}");
        }

        foreach (InstanceType type in sqliteByType.Keys)
        {
            AssertSingletonMatches($"arr_configs[{type}]", sqliteByType[type], postgresByType[type]);
        }
    }

    private static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, JsonOptions);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        DefaultJsonTypeInfoResolver resolver = new();
        resolver.Modifiers.Add(typeInfo =>
        {
            foreach (JsonPropertyInfo property in typeInfo.Properties.ToList())
            {
                if (string.Equals(property.Name, "Id", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(property.Name, "EncryptionKey", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(property.Name, "Instances", StringComparison.OrdinalIgnoreCase))
                {
                    typeInfo.Properties.Remove(property);
                }
            }
        });

        return new JsonSerializerOptions
        {
            TypeInfoResolver = resolver,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            WriteIndented = false,
        };
    }

    private sealed class SeedSnapshot
    {
        public required GeneralConfig GeneralConfig { get; init; }

        public required QueueCleanerConfig QueueCleanerConfig { get; init; }

        public required ContentBlockerConfig ContentBlockerConfig { get; init; }

        public required DownloadCleanerConfig DownloadCleanerConfig { get; init; }

        public required SeekerConfig SeekerConfig { get; init; }

        public required BlacklistSyncConfig BlacklistSyncConfig { get; init; }

        public required List<ArrConfig> ArrConfigs { get; init; }
    }
}
