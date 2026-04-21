using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Cleanuparr.Persistence.Models.Configuration.General;
using Cleanuparr.Persistence.Models.Configuration.MalwareBlocker;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Cleanuparr.Persistence.Models.Configuration.Seeker;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Api.Tests.Features.Seeker.TestHelpers;

/// <summary>
/// Factory for creating SQLite in-memory contexts for Seeker controller tests
/// </summary>
public static class SeekerTestDataFactory
{
    public static DataContext CreateDataContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<DataContext>()
            .UseSqlite(connection)
            .UseLowerCaseNamingConvention()
            .UseSnakeCaseNamingConvention()
            .Options;

        var context = new DataContext(options);
        context.Database.EnsureCreated();

        SeedDefaultData(context);
        return context;
    }

    public static EventsContext CreateEventsContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<EventsContext>()
            .UseSqlite(connection)
            .UseLowerCaseNamingConvention()
            .UseSnakeCaseNamingConvention()
            .Options;

        var context = new EventsContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static void SeedDefaultData(DataContext context)
    {
        context.GeneralConfigs.Add(new GeneralConfig
        {
            Id = Guid.NewGuid(),
            DryRun = false,
            IgnoredDownloads = [],
            Log = new LoggingConfig()
        });

        context.ArrConfigs.AddRange(
            new ArrConfig { Id = Guid.NewGuid(), Type = InstanceType.Sonarr, Instances = [], FailedImportMaxStrikes = 3 },
            new ArrConfig { Id = Guid.NewGuid(), Type = InstanceType.Radarr, Instances = [], FailedImportMaxStrikes = 3 },
            new ArrConfig { Id = Guid.NewGuid(), Type = InstanceType.Lidarr, Instances = [], FailedImportMaxStrikes = 3 },
            new ArrConfig { Id = Guid.NewGuid(), Type = InstanceType.Readarr, Instances = [], FailedImportMaxStrikes = 3 },
            new ArrConfig { Id = Guid.NewGuid(), Type = InstanceType.Whisparr, Instances = [], FailedImportMaxStrikes = 3 }
        );

        context.QueueCleanerConfigs.Add(new QueueCleanerConfig
        {
            Id = Guid.NewGuid(),
            IgnoredDownloads = [],
            FailedImport = new FailedImportConfig()
        });

        context.ContentBlockerConfigs.Add(new ContentBlockerConfig
        {
            Id = Guid.NewGuid(),
            IgnoredDownloads = [],
            DeletePrivate = false,
            Sonarr = new BlocklistSettings { Enabled = false },
            Radarr = new BlocklistSettings { Enabled = false },
            Lidarr = new BlocklistSettings { Enabled = false },
            Readarr = new BlocklistSettings { Enabled = false },
            Whisparr = new BlocklistSettings { Enabled = false }
        });

        context.DownloadCleanerConfigs.Add(new DownloadCleanerConfig
        {
            Id = Guid.NewGuid(),
            IgnoredDownloads = []
        });

        context.SeekerConfigs.Add(new SeekerConfig
        {
            Id = Guid.NewGuid(),
            SearchEnabled = true,
            ProactiveSearchEnabled = false
        });

        context.SaveChanges();
    }

    public static ArrInstance AddSonarrInstance(DataContext context, bool enabled = true)
    {
        var arrConfig = context.ArrConfigs.First(x => x.Type == InstanceType.Sonarr);
        var instance = new ArrInstance
        {
            Id = Guid.NewGuid(),
            Name = "Test Sonarr",
            Url = new Uri("http://sonarr:8989"),
            ApiKey = "test-api-key",
            Enabled = enabled,
            ArrConfigId = arrConfig.Id,
            ArrConfig = arrConfig
        };

        arrConfig.Instances.Add(instance);
        context.ArrInstances.Add(instance);
        context.SaveChanges();
        return instance;
    }

    public static ArrInstance AddRadarrInstance(DataContext context, bool enabled = true)
    {
        var arrConfig = context.ArrConfigs.First(x => x.Type == InstanceType.Radarr);
        var instance = new ArrInstance
        {
            Id = Guid.NewGuid(),
            Name = "Test Radarr",
            Url = new Uri("http://radarr:7878"),
            ApiKey = "test-api-key",
            Enabled = enabled,
            ArrConfigId = arrConfig.Id,
            ArrConfig = arrConfig
        };

        arrConfig.Instances.Add(instance);
        context.ArrInstances.Add(instance);
        context.SaveChanges();
        return instance;
    }

    public static ArrInstance AddLidarrInstance(DataContext context, bool enabled = true)
    {
        var arrConfig = context.ArrConfigs.First(x => x.Type == InstanceType.Lidarr);
        var instance = new ArrInstance
        {
            Id = Guid.NewGuid(),
            Name = "Test Lidarr",
            Url = new Uri("http://lidarr:8686"),
            ApiKey = "test-api-key",
            Enabled = enabled,
            ArrConfigId = arrConfig.Id,
            ArrConfig = arrConfig
        };

        arrConfig.Instances.Add(instance);
        context.ArrInstances.Add(instance);
        context.SaveChanges();
        return instance;
    }
}
