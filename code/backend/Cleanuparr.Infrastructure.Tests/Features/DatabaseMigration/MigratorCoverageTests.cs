using Cleanuparr.Infrastructure.Features.DatabaseMigration;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Providers;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DatabaseMigration;

public class MigratorCoverageTests
{
    [Fact]
    public void Migrator_covers_every_dbcontext_in_the_persistence_assembly()
    {
        HashSet<Type> migratedContexts = new() { typeof(DataContext), typeof(EventsContext), typeof(UsersContext) };

        List<Type> allContexts = typeof(DataContext).Assembly
            .GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && typeof(DbContext).IsAssignableFrom(type))
            .ToList();

        allContexts.ShouldAllBe(type => migratedContexts.Contains(type));
    }

    [Fact]
    public void Instantiate_constructs_every_context_through_the_reflection_ctor()
    {
        IDatabaseProvider provider = new SqliteDatabaseProvider();

        using DataContext data = SqliteToPostgresMigrator.Instantiate(new DbContextOptionsBuilder<DataContext>(), provider);
        using EventsContext events = SqliteToPostgresMigrator.Instantiate(new DbContextOptionsBuilder<EventsContext>(), provider);
        using UsersContext users = SqliteToPostgresMigrator.Instantiate(new DbContextOptionsBuilder<UsersContext>(), provider);

        data.ShouldNotBeNull();
        events.ShouldNotBeNull();
        users.ShouldNotBeNull();
    }
}
