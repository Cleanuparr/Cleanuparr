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

    public async Task<MigrationResult> RunAsync(bool force, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        Dictionary<string, int> counts = new();

        try
        {
            string connectionString = BuildPostgresConnectionString();

            if (!force)
            {
                IReadOnlyList<string> provisioned = await GetProvisionedSchemasAsync(connectionString, cancellationToken);
                if (provisioned.Count > 0)
                {
                    return new MigrationResult(
                        false,
                        $"Target PostgreSQL already contains applied migrations in schema(s): {string.Join(", ", provisioned)}. Re-run with --force to wipe and re-import.",
                        new Dictionary<string, int>());
                }
            }

            await MigrateSourceSchemaAsync(progress, cancellationToken);

            await MigrateTargetSchemaAsync(connectionString, cancellationToken);

            await using NpgsqlConnection connection = new(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using DbTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                await CopyContextAsync<DataContext>(DbContextKind.Data, connection, transaction, counts, progress, cancellationToken);
                await CopyContextAsync<EventsContext>(DbContextKind.Events, connection, transaction, counts, progress, cancellationToken);
                await CopyContextAsync<UsersContext>(DbContextKind.Users, connection, transaction, counts, progress, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }

            return new MigrationResult(true, null, counts);
        }
        catch (Exception exception)
        {
            return new MigrationResult(false, exception.ToString(), counts);
        }
    }

    private static string BuildPostgresConnectionString() =>
        PostgresDatabaseProvider.BuildConnectionString(
            DatabaseConfigProvider.GetRequired(ConfigurationKeys.PostgresHost),
            DatabaseConfigProvider.GetOptional(ConfigurationKeys.PostgresPort),
            DatabaseConfigProvider.GetRequired(ConfigurationKeys.PostgresUser),
            DatabaseConfigProvider.GetRequired(ConfigurationKeys.PostgresPassword),
            DatabaseConfigProvider.GetRequired(ConfigurationKeys.PostgresDatabase),
            DatabaseConfigProvider.GetOptional(ConfigurationKeys.PostgresExtraParams));

    private async Task MigrateSourceSchemaAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        progress?.Report("Applying pending SQLite migrations to the source databases...");

        await using (DataContext data = BuildSourceContext<DataContext>(DbContextKind.Data))
        {
            await data.Database.MigrateAsync(cancellationToken);
        }

        await using (EventsContext events = BuildSourceContext<EventsContext>(DbContextKind.Events))
        {
            await events.Database.MigrateAsync(cancellationToken);
        }

        await using (UsersContext users = BuildSourceContext<UsersContext>(DbContextKind.Users))
        {
            await users.Database.MigrateAsync(cancellationToken);
        }
    }

    private async Task MigrateTargetSchemaAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using DataContext data = BuildTargetContext<DataContext>(connectionString, DbContextKind.Data, keepKeys: false);
        await data.Database.MigrateAsync(cancellationToken);
        await using EventsContext events = BuildTargetContext<EventsContext>(connectionString, DbContextKind.Events, keepKeys: false);
        await events.Database.MigrateAsync(cancellationToken);
        await using UsersContext users = BuildTargetContext<UsersContext>(connectionString, DbContextKind.Users, keepKeys: false);
        await users.Database.MigrateAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<string>> GetProvisionedSchemasAsync(string connectionString, CancellationToken cancellationToken)
    {
        PostgresDatabaseProvider provider = new();
        List<string> provisioned = new();

        if (await HasAppliedMigrationsAsync<DataContext>(connectionString, DbContextKind.Data, cancellationToken))
        {
            provisioned.Add(provider.GetSchema(DbContextKind.Data)!);
        }

        if (await HasAppliedMigrationsAsync<EventsContext>(connectionString, DbContextKind.Events, cancellationToken))
        {
            provisioned.Add(provider.GetSchema(DbContextKind.Events)!);
        }

        if (await HasAppliedMigrationsAsync<UsersContext>(connectionString, DbContextKind.Users, cancellationToken))
        {
            provisioned.Add(provider.GetSchema(DbContextKind.Users)!);
        }

        return provisioned;
    }

    private static async Task<bool> HasAppliedMigrationsAsync<TContext>(string connectionString, DbContextKind kind, CancellationToken cancellationToken)
        where TContext : DbContext
    {
        await using TContext context = BuildTargetContext<TContext>(connectionString, kind, keepKeys: false);
        IEnumerable<string> applied = await context.Database.GetAppliedMigrationsAsync(cancellationToken);
        return applied.Any();
    }

    private async Task CopyContextAsync<TContext>(
        DbContextKind kind,
        NpgsqlConnection connection,
        DbTransaction transaction,
        Dictionary<string, int> counts,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
        where TContext : DbContext
    {
        await using TContext source = BuildSourceContext<TContext>(kind);
        await using TContext target = BuildTargetContextOnConnection<TContext>(connection, kind);
        await target.Database.UseTransactionAsync(transaction, cancellationToken);

        await TruncateAsync(target, kind, cancellationToken);
        await _copier.CopyAsync(source, target, progress, cancellationToken);
        await VerifyAndRecordCountsAsync(source, target, counts, cancellationToken);
    }

    public static TContext Instantiate<TContext>(DbContextOptionsBuilder<TContext> builder, IDatabaseProvider provider) where TContext : DbContext =>
        (TContext)Activator.CreateInstance(typeof(TContext), builder.Options, provider)!;

    private static TContext BuildSourceContext<TContext>(DbContextKind kind) where TContext : DbContext
    {
        SqliteDatabaseProvider provider = new();
        DbContextOptionsBuilder<TContext> builder = new();
        provider.ConfigureContext(builder, kind);
        return Instantiate(builder, provider);
    }

    private static TContext BuildTargetContext<TContext>(string connectionString, DbContextKind kind, bool keepKeys) where TContext : DbContext
    {
        PostgresDatabaseProvider provider = new();
        string schema = provider.GetSchema(kind)!;
        DbContextOptionsBuilder<TContext> builder = new();
        builder.UseNpgsql(connectionString, options => options
                .MigrationsAssembly(PostgresDatabaseProvider.MigrationsAssembly)
                .MigrationsHistoryTable("__ef_migrations_history", schema))
            .UseLowerCaseNamingConvention()
            .UseSnakeCaseNamingConvention();
        if (keepKeys)
        {
            builder.ReplaceService<IModelCustomizer, KeepKeysModelCustomizer>();
        }
        return Instantiate(builder, provider);
    }

    private static TContext BuildTargetContextOnConnection<TContext>(NpgsqlConnection connection, DbContextKind kind) where TContext : DbContext
    {
        PostgresDatabaseProvider provider = new();
        string schema = provider.GetSchema(kind)!;
        DbContextOptionsBuilder<TContext> builder = new();
        builder.UseNpgsql(connection, options => options
                .MigrationsAssembly(PostgresDatabaseProvider.MigrationsAssembly)
                .MigrationsHistoryTable("__ef_migrations_history", schema))
            .UseLowerCaseNamingConvention()
            .UseSnakeCaseNamingConvention()
            .ReplaceService<IModelCustomizer, KeepKeysModelCustomizer>();
        return Instantiate(builder, provider);
    }

    private static async Task TruncateAsync(DbContext target, DbContextKind kind, CancellationToken cancellationToken)
    {
        string schema = new PostgresDatabaseProvider().GetSchema(kind)!;
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

    internal static async Task VerifyAndRecordCountsAsync(
        DbContext source,
        DbContext target,
        Dictionary<string, int> counts,
        CancellationToken cancellationToken)
    {
        foreach (IEntityType entityType in ModelDataCopier.OrderByDependencies(target.Model.GetEntityTypes()))
        {
            string? table = entityType.GetTableName();
            if (table is null)
            {
                continue;
            }

            int sourceCount = await CountAsync(source, entityType.ClrType, cancellationToken);
            int targetCount = await CountAsync(target, entityType.ClrType, cancellationToken);

            if (sourceCount != targetCount)
            {
                throw new InvalidOperationException(
                    $"Row count mismatch for table '{table}': source has {sourceCount} rows, target has {targetCount} rows.");
            }

            counts[table] = targetCount;
        }
    }

    private static async Task<int> CountAsync(DbContext context, Type entityClrType, CancellationToken cancellationToken)
    {
        MethodInfo setMethod = typeof(DbContext)
            .GetMethods()
            .Single(method => method.Name == nameof(DbContext.Set) && method.IsGenericMethod && method.GetParameters().Length == 0)
            .MakeGenericMethod(entityClrType);

        object dbSet = setMethod.Invoke(context, null)!;

        MethodInfo countMethod = typeof(EntityFrameworkQueryableExtensions)
            .GetMethods()
            .Single(method =>
                method.Name == nameof(EntityFrameworkQueryableExtensions.CountAsync) &&
                method.GetParameters().Length == 2)
            .MakeGenericMethod(entityClrType);

        object task = countMethod.Invoke(null, new object[] { dbSet, cancellationToken })!;
        return await (Task<int>)task;
    }
}
