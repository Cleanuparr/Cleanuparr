using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DatabaseMigration;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Providers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
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
}
