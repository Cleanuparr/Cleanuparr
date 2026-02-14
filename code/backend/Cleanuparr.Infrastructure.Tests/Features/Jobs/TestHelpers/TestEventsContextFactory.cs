using Cleanuparr.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Infrastructure.Tests.Features.Jobs.TestHelpers;

/// <summary>
/// Factory for creating SQLite in-memory EventsContext instances for testing.
/// SQLite in-memory supports ExecuteUpdateAsync, ExecuteDeleteAsync, and EF.Functions.Like,
/// unlike the EF Core InMemory provider.
/// </summary>
public static class TestEventsContextFactory
{
    /// <summary>
    /// Creates a new SQLite in-memory EventsContext with schema initialized
    /// </summary>
    public static EventsContext Create()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<EventsContext>()
            .UseSqlite(connection)
            .Options;

        var context = new EventsContext(options);
        context.Database.EnsureCreated();

        return context;
    }
}
