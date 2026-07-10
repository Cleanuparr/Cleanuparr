using Cleanuparr.Persistence.Converters;
using Cleanuparr.Shared.Enums;
using Cleanuparr.Shared.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Cleanuparr.Persistence.Providers;

public sealed class SqliteDatabaseProvider : IDatabaseProvider
{
    private static readonly IReadOnlyDictionary<DbContextKind, string> FileNames = new Dictionary<DbContextKind, string>
    {
        [DbContextKind.Data] = "cleanuparr.db",
        [DbContextKind.Events] = "events.db",
        [DbContextKind.Users] = "users.db",
    };

    public DatabaseProvider Kind => DatabaseProvider.Sqlite;

    public void ConfigureContext(DbContextOptionsBuilder builder, DbContextKind kind)
    {
        string dbPath = Path.Combine(ConfigurationPathProvider.GetConfigPath(), FileNames[kind]);
        builder
            .UseSqlite($"Data Source={dbPath}")
            .UseLowerCaseNamingConvention()
            .UseSnakeCaseNamingConvention();
    }

    public string? GetSchema(DbContextKind kind) => null;

    public void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<DateTimeOffset>()
            .HaveConversion<UtcDateTimeOffsetConverter>();
    }

    public string GetUnresolvedEventFilter() => "\"is_resolved\" = 0";

    public async Task OnPostMigrateAsync(DatabaseFacade database, DbContextKind kind, CancellationToken cancellationToken)
    {
        if (kind == DbContextKind.Events)
        {
            await database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);
        }
    }

    public async Task PrepareBulkWriteAsync(DatabaseFacade database, CancellationToken cancellationToken)
    {
        await database.ExecuteSqlRawAsync("PRAGMA cache_size=-20000;", cancellationToken);
    }

    public async Task CheckpointAsync(DatabaseFacade database, CancellationToken cancellationToken)
    {
        await database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE);", cancellationToken);
    }

    public string EscapeLikePattern(string input)
    {
        string escaped = input
            .Replace("[", "[[]")
            .Replace("%", "[%]")
            .Replace("_", "[_]");

        return $"%{escaped}%";
    }
}
