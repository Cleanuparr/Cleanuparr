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

    private string? _connectionString;

    public DatabaseProvider Kind => DatabaseProvider.Postgres;

    public void ConfigureContext(DbContextOptionsBuilder builder, DbContextKind kind)
    {
        builder
            .UseNpgsql(GetConnectionString(), options => options.MigrationsAssembly(MigrationsAssembly))
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

    public string EscapeLikePattern(string input)
    {
        string escaped = input
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");

        return $"%{escaped}%";
    }

    private string GetConnectionString()
    {
        _connectionString ??= BuildConnectionString(
            DatabaseConfigProvider.GetRequired("POSTGRES_HOST"),
            DatabaseConfigProvider.GetOptional("POSTGRES_PORT"),
            DatabaseConfigProvider.GetRequired("POSTGRES_USER"),
            DatabaseConfigProvider.GetRequired("POSTGRES_PASS"),
            DatabaseConfigProvider.GetRequired("POSTGRES_DB"),
            DatabaseConfigProvider.GetOptional("POSTGRES_EXTRA_PARAMS"));

        return _connectionString;
    }

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
