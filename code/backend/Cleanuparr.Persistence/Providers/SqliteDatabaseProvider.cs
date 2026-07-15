using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Converters;
using Cleanuparr.Shared.Enums;
using Cleanuparr.Shared.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Cleanuparr.Persistence.Providers;

public sealed class SqliteDatabaseProvider : IDatabaseProvider
{
    private const string MigrationsAssembly = "Cleanuparr.Persistence.Sqlite";

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
            .UseSqlite($"Data Source={dbPath}", options => options.MigrationsAssembly(MigrationsAssembly))
            .UseLowerCaseNamingConvention()
            .UseSnakeCaseNamingConvention();
    }

    public string? GetSchema(DbContextKind kind) => null;

    public string QualifyTable(string tableName, DbContextKind kind) => tableName;

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

    public string GetTimelineBucketExpr(TimelineBucketSize size) => size switch
    {
        TimelineBucketSize.Hour => "substr(timestamp, 1, 13)",
        TimelineBucketSize.Day => "substr(timestamp, 1, 10)",
        TimelineBucketSize.Week => "date(substr(timestamp, 1, 19), '-' || ((strftime('%w', substr(timestamp, 1, 19)) + 6) % 7) || ' days')",
        TimelineBucketSize.Month => "strftime('%Y-%m-01', substr(timestamp, 1, 19))",
        _ => throw new ArgumentOutOfRangeException(nameof(size), size, null),
    };

    public bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqliteException { SqliteErrorCode: 19 };
}
