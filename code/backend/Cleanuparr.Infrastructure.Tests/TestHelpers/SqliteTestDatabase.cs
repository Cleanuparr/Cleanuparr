using Cleanuparr.Persistence.Providers;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Infrastructure.Tests.TestHelpers;

/// <summary>
/// A throwaway file-backed SQLite database, wired the way the app wires one.
/// Dispose deletes the file and its journals.
/// </summary>
public sealed class SqliteTestDatabase : IDisposable
{
    private readonly string _path;

    private SqliteTestDatabase(string path)
    {
        _path = path;
    }

    /// <summary>
    /// The provider the contexts were built with.
    /// </summary>
    public SqliteDatabaseProvider Provider { get; } = new();

    /// <summary>
    /// Names the temp file after the caller.
    /// </summary>
    public static SqliteTestDatabase Create(string name) =>
        new(Path.Combine(Path.GetTempPath(), $"cleanuparr-{name}-{Guid.NewGuid():N}.db"));

    /// <summary>
    /// A context pointing at this database, unmigrated.
    /// </summary>
    public TContext CreateContext<TContext>()
        where TContext : DbContext
    {
        DbContextOptionsBuilder<TContext> optionsBuilder = new();
        optionsBuilder
            .UseSqlite(
                $"Data Source={_path}",
                options => options.MigrationsAssembly("Cleanuparr.Persistence.Sqlite"))
            .UseLowerCaseNamingConvention()
            .UseSnakeCaseNamingConvention();

        return (TContext)Activator.CreateInstance(
            typeof(TContext),
            optionsBuilder.Options,
            Provider)!;
    }

    public void Dispose()
    {
        foreach (string file in new[] { _path, $"{_path}-wal", $"{_path}-shm" })
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }
}
