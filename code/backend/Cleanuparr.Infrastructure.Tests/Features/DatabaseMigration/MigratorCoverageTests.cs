using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DatabaseMigration;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Providers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DatabaseMigration;

public class MigratorCoverageTests
{
    [Fact]
    public async Task VerifyAndRecordCounts_throws_when_source_and_target_row_counts_differ()
    {
        SqliteConnection sourceConnection = new("DataSource=:memory:");
        sourceConnection.Open();
        SqliteConnection targetConnection = new("DataSource=:memory:");
        targetConnection.Open();

        SqliteDatabaseProvider provider = new();
        DbContextOptions<DataContext> sourceOptions = new DbContextOptionsBuilder<DataContext>()
            .UseSqlite(sourceConnection)
            .UseSnakeCaseNamingConvention()
            .Options;
        DbContextOptions<DataContext> targetOptions = new DbContextOptionsBuilder<DataContext>()
            .UseSqlite(targetConnection)
            .UseSnakeCaseNamingConvention()
            .Options;

        await using DataContext source = new(sourceOptions, provider);
        await using DataContext target = new(targetOptions, provider);
        await source.Database.EnsureCreatedAsync();
        await target.Database.EnsureCreatedAsync();

        source.ArrConfigs.Add(new ArrConfig { Id = Guid.NewGuid(), Type = InstanceType.Sonarr });
        await source.SaveChangesAsync();

        Dictionary<string, int> counts = new();
        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => SqliteToPostgresMigrator.VerifyAndRecordCountsAsync(source, target, counts, CancellationToken.None));
        exception.Message.ShouldContain("Row count mismatch");
    }

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
