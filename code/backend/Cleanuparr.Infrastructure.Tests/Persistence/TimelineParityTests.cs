using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Events;
using Cleanuparr.Persistence.Providers;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Persistence;

[Collection("SeedParity")]
public class TimelineParityTests
{
    private static readonly DateTimeOffset[] SeedTimestamps =
    {
        new(2026, 1, 4, 10, 0, 0, TimeSpan.Zero),
        new(2026, 1, 5, 10, 0, 0, TimeSpan.Zero),
        new(2026, 1, 5, 15, 0, 0, TimeSpan.Zero),
        new(2026, 2, 15, 8, 30, 0, TimeSpan.Zero),
    };

    [SkippableTheory]
    [InlineData(TimelineBucketSize.Hour)]
    [InlineData(TimelineBucketSize.Day)]
    [InlineData(TimelineBucketSize.Week)]
    [InlineData(TimelineBucketSize.Month)]
    public async Task Timeline_bucket_labels_match_across_backends(TimelineBucketSize size)
    {
        PostgreSqlContainer postgresContainer;

        try
        {
            postgresContainer = new PostgreSqlBuilder()
                .WithImage("postgres:17")
                .Build();

            await postgresContainer.StartAsync();
        }
        catch (Exception exception)
        {
            throw new SkipException($"Docker is unavailable, skipping timeline parity test: {exception.Message}");
        }

        string sqlitePath = Path.Combine(Path.GetTempPath(), $"cleanuparr-timeline-parity-{Guid.NewGuid():N}.db");

        try
        {
            List<string> sqliteLabels = await BucketLabelsSqliteAsync(sqlitePath, size);
            List<string> postgresLabels = await BucketLabelsPostgresAsync(postgresContainer, size);

            postgresLabels.ShouldBe(sqliteLabels);
        }
        finally
        {
            await postgresContainer.DisposeAsync();

            if (File.Exists(sqlitePath))
            {
                File.Delete(sqlitePath);
            }
        }
    }

    private static async Task<List<string>> BucketLabelsSqliteAsync(string sqlitePath, TimelineBucketSize size)
    {
        SqliteDatabaseProvider provider = new();
        DbContextOptionsBuilder<EventsContext> builder = new();
        builder
            .UseSqlite($"Data Source={sqlitePath}", options => options.MigrationsAssembly("Cleanuparr.Persistence.Sqlite"))
            .UseLowerCaseNamingConvention()
            .UseSnakeCaseNamingConvention();

        await using EventsContext context = new(builder.Options, provider);
        await context.Database.MigrateAsync();
        await SeedAsync(context);

        return await BucketLabelsAsync(context, provider, size);
    }

    private static async Task<List<string>> BucketLabelsPostgresAsync(PostgreSqlContainer container, TimelineBucketSize size)
    {
        PostgresDatabaseProvider provider = new();
        DbContextOptionsBuilder<EventsContext> builder = new();
        builder
            .UseNpgsql(container.GetConnectionString(), options => options.MigrationsAssembly(PostgresDatabaseProvider.MigrationsAssembly))
            .UseLowerCaseNamingConvention()
            .UseSnakeCaseNamingConvention();

        await using EventsContext context = new(builder.Options, provider);
        await context.Database.MigrateAsync();
        await SeedAsync(context);

        return await BucketLabelsAsync(context, provider, size);
    }

    private static async Task SeedAsync(EventsContext context)
    {
        foreach (DateTimeOffset timestamp in SeedTimestamps)
        {
            context.Events.Add(new AppEvent
            {
                EventType = EventType.QueueItemDeleted,
                Message = "timeline parity",
                Severity = EventSeverity.Information,
                Timestamp = timestamp,
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task<List<string>> BucketLabelsAsync(EventsContext context, IDatabaseProvider provider, TimelineBucketSize size)
    {
        string bucketExpr = provider.GetTimelineBucketExpr(size);
        string table = provider.QualifyTable("events", DbContextKind.Events);
        string sql = $"SELECT {bucketExpr} AS \"Value\" FROM {table} GROUP BY {bucketExpr} ORDER BY 1";

        return await context.Database.SqlQueryRaw<string>(sql).ToListAsync();
    }
}
