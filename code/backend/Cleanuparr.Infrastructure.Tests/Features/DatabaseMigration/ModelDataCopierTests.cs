using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DatabaseMigration;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Events;
using Cleanuparr.Persistence.Models.State;
using Cleanuparr.Persistence.Providers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DatabaseMigration;

public class ModelDataCopierTests
{
    [Fact]
    public async Task CopyAsync_reproduces_rows_and_preserves_keys()
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
            .ReplaceService<IModelCustomizer, KeepKeysModelCustomizer>()
            .Options;

        Guid arrConfigId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        await using (DataContext source = new(sourceOptions, provider))
        {
            await source.Database.EnsureCreatedAsync();
            source.ArrConfigs.Add(new ArrConfig { Id = arrConfigId, Type = InstanceType.Sonarr });
            await source.SaveChangesAsync();
        }

        await using (DataContext target = new(targetOptions, provider))
        {
            await target.Database.EnsureCreatedAsync();
            await using (DataContext source = new(sourceOptions, provider))
            {
                ModelDataCopier copier = new();
                await copier.CopyAsync(source, target, CancellationToken.None);
            }

            List<ArrConfig> copied = await target.ArrConfigs.AsNoTracking().ToListAsync();
            copied.Count.ShouldBe(1);
            copied[0].Id.ShouldBe(arrConfigId);
            copied[0].Type.ShouldBe(InstanceType.Sonarr);
        }
    }

    [Fact]
    public void OrderByDependencies_orders_principals_before_dependents_in_events_model()
    {
        SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();
        SqliteDatabaseProvider provider = new();
        DbContextOptions<EventsContext> options = new DbContextOptionsBuilder<EventsContext>()
            .UseSqlite(connection)
            .UseSnakeCaseNamingConvention()
            .Options;

        using EventsContext context = new(options, provider);
        List<IEntityType> ordered = ModelDataCopier.OrderByDependencies(context.Model.GetEntityTypes()).ToList();

        int strikeIndex = ordered.FindIndex(entityType => entityType.ClrType == typeof(Strike));
        int jobRunIndex = ordered.FindIndex(entityType => entityType.ClrType == typeof(JobRun));
        int appEventIndex = ordered.FindIndex(entityType => entityType.ClrType == typeof(AppEvent));

        strikeIndex.ShouldBeGreaterThanOrEqualTo(0);
        jobRunIndex.ShouldBeGreaterThanOrEqualTo(0);
        appEventIndex.ShouldBeGreaterThanOrEqualTo(0);
        strikeIndex.ShouldBeLessThan(appEventIndex);
        jobRunIndex.ShouldBeLessThan(appEventIndex);
    }

    [Fact]
    public void OrderByDependencies_throws_on_a_circular_fk_dependency()
    {
        DbContextOptions<CycleContext> options = new DbContextOptionsBuilder<CycleContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        using CycleContext context = new(options);

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => ModelDataCopier.OrderByDependencies(context.Model.GetEntityTypes()));
        exception.Message.ShouldContain("Circular FK dependency");
    }

    private sealed class CycleContext : DbContext
    {
        public CycleContext(DbContextOptions<CycleContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CycleNode>().HasOne(node => node.Edge).WithMany().HasForeignKey(node => node.EdgeId);
            modelBuilder.Entity<CycleEdge>().HasOne(edge => edge.Node).WithMany().HasForeignKey(edge => edge.NodeId);
        }
    }

    private sealed class CycleNode
    {
        public Guid Id { get; set; }
        public Guid? EdgeId { get; set; }
        public CycleEdge? Edge { get; set; }
    }

    private sealed class CycleEdge
    {
        public Guid Id { get; set; }
        public Guid? NodeId { get; set; }
        public CycleNode? Node { get; set; }
    }
}
