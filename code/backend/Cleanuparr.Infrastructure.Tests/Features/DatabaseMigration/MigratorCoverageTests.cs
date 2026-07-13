using Cleanuparr.Persistence;
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
}
