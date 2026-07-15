using Cleanuparr.Domain.Enums;
using Cleanuparr.Shared.Configuration;
using Cleanuparr.Shared.Enums;
using Cleanuparr.Shared.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;

namespace Cleanuparr.Persistence.Providers;

public sealed class PostgresDatabaseProvider : IDatabaseProvider
{
    private const string MigrationsAssembly = "Cleanuparr.Persistence.Postgres";

    private static readonly IReadOnlyDictionary<DbContextKind, string> Schemas = new Dictionary<DbContextKind, string>
    {
        [DbContextKind.Data] = "data",
        [DbContextKind.Events] = "events",
        [DbContextKind.Users] = "users",
    };

    public DatabaseProvider Kind => DatabaseProvider.Postgres;

    public void ConfigureContext(DbContextOptionsBuilder builder, DbContextKind kind)
    {
        string schema = Schemas[kind];

        builder
            .UseNpgsql(GetConnectionString(), options => options
                .MigrationsAssembly(MigrationsAssembly)
                .MigrationsHistoryTable("__ef_migrations_history", schema))
            .UseLowerCaseNamingConvention()
            .UseSnakeCaseNamingConvention();
    }

    public string? GetSchema(DbContextKind kind) => Schemas[kind];

    public void ConfigureConventions(ModelConfigurationBuilder builder)
    {
    }

    public string GetUnresolvedEventFilter() => "is_resolved = false";

    public Task OnPostMigrateAsync(DatabaseFacade database, DbContextKind kind, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task PrepareBulkWriteAsync(DatabaseFacade database, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task CheckpointAsync(DatabaseFacade database, CancellationToken cancellationToken) => Task.CompletedTask;

    public string GetTimelineBucketExpr(TimelineBucketSize size) => size switch
    {
        TimelineBucketSize.Hour => "to_char(\"timestamp\" AT TIME ZONE 'UTC', 'YYYY-MM-DD HH24')",
        TimelineBucketSize.Day => "to_char(\"timestamp\" AT TIME ZONE 'UTC', 'YYYY-MM-DD')",
        TimelineBucketSize.Week => "to_char(date_trunc('week', \"timestamp\" AT TIME ZONE 'UTC'), 'YYYY-MM-DD')",
        TimelineBucketSize.Month => "to_char(date_trunc('month', \"timestamp\" AT TIME ZONE 'UTC'), 'YYYY-MM-DD')",
        _ => throw new ArgumentOutOfRangeException(nameof(size), size, null),
    };

    public bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: "23505" };

    private static string GetConnectionString() =>
        BuildConnectionString(
            DatabaseConfigProvider.GetRequired(ConfigurationKeys.PostgresHost),
            DatabaseConfigProvider.GetOptional(ConfigurationKeys.PostgresPort),
            DatabaseConfigProvider.GetRequired(ConfigurationKeys.PostgresUser),
            DatabaseConfigProvider.GetRequired(ConfigurationKeys.PostgresPassword),
            DatabaseConfigProvider.GetRequired(ConfigurationKeys.PostgresDatabase),
            DatabaseConfigProvider.GetOptional(ConfigurationKeys.PostgresExtraParams));

    public static string BuildConnectionString(string host, string? port, string user, string password, string database, string? extraParams)
    {
        NpgsqlConnectionStringBuilder builder = new()
        {
            Host = host,
            Username = user,
            Password = password,
            Database = database,
        };

        if (!string.IsNullOrWhiteSpace(port))
        {
            builder.Port = int.Parse(port);
        }

        if (!string.IsNullOrWhiteSpace(extraParams))
        {
            NpgsqlConnectionStringBuilder extra = new(extraParams);
            foreach (KeyValuePair<string, object?> pair in extra)
            {
                builder[pair.Key] = pair.Value;
            }
        }

        return builder.ConnectionString;
    }
}
