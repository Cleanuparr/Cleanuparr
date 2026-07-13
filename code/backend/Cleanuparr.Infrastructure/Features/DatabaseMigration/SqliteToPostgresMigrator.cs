using System.Data.Common;
using System.Reflection;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Providers;
using Cleanuparr.Shared.Configuration;
using Cleanuparr.Shared.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;

namespace Cleanuparr.Infrastructure.Features.DatabaseMigration;

public sealed record MigrationResult(bool Success, string? Error, IReadOnlyDictionary<string, int> TableCounts);

public sealed class SqliteToPostgresMigrator
{
    private readonly ModelDataCopier _copier = new();

    public async Task<MigrationResult> RunAsync(bool force, CancellationToken cancellationToken)
    {
        string connectionString;
        try
        {
            connectionString = BuildPostgresConnectionString();
        }
        catch (Exception exception)
        {
            return new MigrationResult(false, exception.Message, new Dictionary<string, int>());
        }

        await MigrateTargetSchemaAsync(connectionString, cancellationToken);

        if (!force && await TargetHasRealDataAsync(connectionString, cancellationToken))
        {
            return new MigrationResult(
                false,
                "Target PostgreSQL already contains data. Re-run with --force to wipe and re-import.",
                new Dictionary<string, int>());
        }

        Dictionary<string, int> counts = new();

        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using DbTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await CopyContextAsync<DataContext>(DbContextKind.Data, connection, transaction, counts, cancellationToken);
            await CopyContextAsync<EventsContext>(DbContextKind.Events, connection, transaction, counts, cancellationToken);
            await CopyContextAsync<UsersContext>(DbContextKind.Users, connection, transaction, counts, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new MigrationResult(false, exception.Message, counts);
        }

        return new MigrationResult(true, null, counts);
    }

    private static string BuildPostgresConnectionString() =>
        PostgresDatabaseProvider.BuildConnectionString(
            DatabaseConfigProvider.GetRequired(ConfigurationKeys.PostgresHost),
            DatabaseConfigProvider.GetOptional(ConfigurationKeys.PostgresPort),
            DatabaseConfigProvider.GetRequired(ConfigurationKeys.PostgresUser),
            DatabaseConfigProvider.GetRequired(ConfigurationKeys.PostgresPassword),
            DatabaseConfigProvider.GetRequired(ConfigurationKeys.PostgresDatabase),
            DatabaseConfigProvider.GetOptional(ConfigurationKeys.PostgresExtraParams));

    private async Task MigrateTargetSchemaAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using DataContext data = BuildTargetContext<DataContext>(connectionString, keepKeys: false);
        await data.Database.MigrateAsync(cancellationToken);
        await using EventsContext events = BuildTargetContext<EventsContext>(connectionString, keepKeys: false);
        await events.Database.MigrateAsync(cancellationToken);
        await using UsersContext users = BuildTargetContext<UsersContext>(connectionString, keepKeys: false);
        await users.Database.MigrateAsync(cancellationToken);
    }

    private static async Task<bool> TargetHasRealDataAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using EventsContext events = BuildTargetContext<EventsContext>(connectionString, keepKeys: false);
        if (await events.Events.AnyAsync(cancellationToken) || await events.Strikes.AnyAsync(cancellationToken))
        {
            return true;
        }

        await using UsersContext users = BuildTargetContext<UsersContext>(connectionString, keepKeys: false);
        return await users.Users.AnyAsync(cancellationToken);
    }

    private async Task CopyContextAsync<TContext>(
        DbContextKind kind,
        NpgsqlConnection connection,
        DbTransaction transaction,
        Dictionary<string, int> counts,
        CancellationToken cancellationToken)
        where TContext : DbContext
    {
        await using TContext source = BuildSourceContext<TContext>(kind);
        await using TContext target = BuildTargetContextOnConnection<TContext>(connection);
        await target.Database.UseTransactionAsync(transaction, cancellationToken);

        await TruncateAsync(target, kind, cancellationToken);
        await _copier.CopyAsync(source, target, cancellationToken);
        RecordCounts(target, counts);
    }

    private static TContext BuildSourceContext<TContext>(DbContextKind kind) where TContext : DbContext
    {
        string fileName = kind switch
        {
            DbContextKind.Data => "cleanuparr.db",
            DbContextKind.Events => "events.db",
            DbContextKind.Users => "users.db",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        string path = Path.Combine(ConfigurationPathProvider.GetConfigPath(), fileName);
        SqliteDatabaseProvider provider = new();
        DbContextOptionsBuilder<TContext> builder = new();
        builder.UseSqlite($"Data Source={path}", options => options.MigrationsAssembly("Cleanuparr.Persistence.Sqlite"))
            .UseLowerCaseNamingConvention()
            .UseSnakeCaseNamingConvention();
        return (TContext)Activator.CreateInstance(typeof(TContext), builder.Options, provider)!;
    }

    private static TContext BuildTargetContext<TContext>(string connectionString, bool keepKeys) where TContext : DbContext
    {
        PostgresDatabaseProvider provider = new();
        DbContextOptionsBuilder<TContext> builder = new();
        builder.UseNpgsql(connectionString, options => options.MigrationsAssembly("Cleanuparr.Persistence.Postgres"))
            .UseLowerCaseNamingConvention()
            .UseSnakeCaseNamingConvention();
        if (keepKeys)
        {
            builder.ReplaceService<IModelCustomizer, KeepKeysModelCustomizer>();
        }
        return (TContext)Activator.CreateInstance(typeof(TContext), builder.Options, provider)!;
    }

    private static TContext BuildTargetContextOnConnection<TContext>(NpgsqlConnection connection) where TContext : DbContext
    {
        PostgresDatabaseProvider provider = new();
        DbContextOptionsBuilder<TContext> builder = new();
        builder.UseNpgsql(connection, options => options.MigrationsAssembly("Cleanuparr.Persistence.Postgres"))
            .UseLowerCaseNamingConvention()
            .UseSnakeCaseNamingConvention()
            .ReplaceService<IModelCustomizer, KeepKeysModelCustomizer>();
        return (TContext)Activator.CreateInstance(typeof(TContext), builder.Options, provider)!;
    }

    private static async Task TruncateAsync(DbContext target, DbContextKind kind, CancellationToken cancellationToken)
    {
        string schema = kind switch
        {
            DbContextKind.Data => "data",
            DbContextKind.Events => "events",
            DbContextKind.Users => "users",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        List<string> tables = target.Model.GetEntityTypes()
            .Where(entityType => !entityType.IsOwned())
            .Select(entityType => entityType.GetTableName())
            .Where(name => name is not null)
            .Distinct()
            .Select(name => $"\"{schema}\".\"{name}\"")
            .ToList();

        if (tables.Count == 0)
        {
            return;
        }

        string sql = $"TRUNCATE TABLE {string.Join(", ", tables)} CASCADE;";
        await target.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private static void RecordCounts(DbContext target, Dictionary<string, int> counts)
    {
        foreach (IEntityType entityType in ModelDataCopier.OrderByDependencies(target.Model.GetEntityTypes()))
        {
            string? table = entityType.GetTableName();
            if (table is null)
            {
                continue;
            }

            MethodInfo setMethod = typeof(DbContext)
                .GetMethods()
                .Single(method => method.Name == nameof(DbContext.Set) && method.IsGenericMethod && method.GetParameters().Length == 0)
                .MakeGenericMethod(entityType.ClrType);

            object dbSet = setMethod.Invoke(target, null)!;
            counts[table] = ((IQueryable<object>)dbSet).Count();
        }
    }
}
