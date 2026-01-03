using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Cleanuparr.Persistence.Models.Configuration.General;
using Cleanuparr.Persistence.Models.Configuration.MalwareBlocker;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Infrastructure.Tests.Features.Jobs.TestHelpers;

/// <summary>
/// Factory for creating SQLite in-memory DataContext instances for testing
/// </summary>
public static class TestDataContextFactory
{
    /// <summary>
    /// Creates a new SQLite in-memory DataContext with default seed data
    /// </summary>
    public static DataContext Create(bool seedData = true)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<DataContext>()
            .UseSqlite(connection)
            .Options;

        var context = new DataContext(options);
        context.Database.EnsureCreated();

        if (seedData)
        {
            SeedDefaultData(context);
        }

        return context;
    }

    /// <summary>
    /// Seeds the minimum required data for GenericHandler.ExecuteAsync() to work
    /// </summary>
    private static void SeedDefaultData(DataContext context)
    {
        // General config
        context.GeneralConfigs.Add(new GeneralConfig
        {
            Id = Guid.NewGuid(),
            DryRun = false,
            IgnoredDownloads = [],
            Log = new LoggingConfig()
        });

        // Arr configs for all instance types
        context.ArrConfigs.AddRange(
            new ArrConfig { Id = Guid.NewGuid(), Type = InstanceType.Sonarr, Instances = [], FailedImportMaxStrikes = 3 },
            new ArrConfig { Id = Guid.NewGuid(), Type = InstanceType.Radarr, Instances = [], FailedImportMaxStrikes = 3 },
            new ArrConfig { Id = Guid.NewGuid(), Type = InstanceType.Lidarr, Instances = [], FailedImportMaxStrikes = 3 },
            new ArrConfig { Id = Guid.NewGuid(), Type = InstanceType.Readarr, Instances = [], FailedImportMaxStrikes = 3 },
            new ArrConfig { Id = Guid.NewGuid(), Type = InstanceType.Whisparr, Instances = [], FailedImportMaxStrikes = 3 }
        );

        // Queue cleaner config
        context.QueueCleanerConfigs.Add(new QueueCleanerConfig
        {
            Id = Guid.NewGuid(),
            IgnoredDownloads = [],
            FailedImport = new FailedImportConfig()
        });

        // Content blocker config
        context.ContentBlockerConfigs.Add(new ContentBlockerConfig
        {
            Id = Guid.NewGuid(),
            IgnoredDownloads = [],
            DeleteKnownMalware = false,
            DeletePrivate = false,
            Sonarr = new BlocklistSettings { Enabled = false },
            Radarr = new BlocklistSettings { Enabled = false },
            Lidarr = new BlocklistSettings { Enabled = false },
            Readarr = new BlocklistSettings { Enabled = false },
            Whisparr = new BlocklistSettings { Enabled = false }
        });

        // Download cleaner config
        context.DownloadCleanerConfigs.Add(new DownloadCleanerConfig
        {
            Id = Guid.NewGuid(),
            IgnoredDownloads = [],
            Categories = [],
            UnlinkedEnabled = false,
            UnlinkedTargetCategory = "",
            UnlinkedCategories = []
        });

        context.SaveChanges();
    }

    /// <summary>
    /// Adds an enabled Sonarr instance to the context
    /// </summary>
    public static ArrInstance AddSonarrInstance(DataContext context, string url = "http://sonarr:8989", bool enabled = true)
    {
        var arrConfig = context.ArrConfigs.First(x => x.Type == InstanceType.Sonarr);
        var instance = new ArrInstance
        {
            Id = Guid.NewGuid(),
            Name = "Test Sonarr",
            Url = new Uri(url),
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

    /// <summary>
    /// Adds an enabled Radarr instance to the context
    /// </summary>
    public static ArrInstance AddRadarrInstance(DataContext context, string url = "http://radarr:7878", bool enabled = true)
    {
        var arrConfig = context.ArrConfigs.First(x => x.Type == InstanceType.Radarr);
        var instance = new ArrInstance
        {
            Id = Guid.NewGuid(),
            Name = "Test Radarr",
            Url = new Uri(url),
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

    /// <summary>
    /// Adds an enabled Lidarr instance to the context
    /// </summary>
    public static ArrInstance AddLidarrInstance(DataContext context, string url = "http://lidarr:8686", bool enabled = true)
    {
        var arrConfig = context.ArrConfigs.First(x => x.Type == InstanceType.Lidarr);
        var instance = new ArrInstance
        {
            Id = Guid.NewGuid(),
            Name = "Test Lidarr",
            Url = new Uri(url),
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

    /// <summary>
    /// Adds an enabled Readarr instance to the context
    /// </summary>
    public static ArrInstance AddReadarrInstance(DataContext context, string url = "http://readarr:8787", bool enabled = true)
    {
        var arrConfig = context.ArrConfigs.First(x => x.Type == InstanceType.Readarr);
        var instance = new ArrInstance
        {
            Id = Guid.NewGuid(),
            Name = "Test Readarr",
            Url = new Uri(url),
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

    /// <summary>
    /// Adds an enabled Whisparr instance to the context
    /// </summary>
    public static ArrInstance AddWhisparrInstance(DataContext context, string url = "http://whisparr:6969", bool enabled = true, float version = 2)
    {
        var arrConfig = context.ArrConfigs.First(x => x.Type == InstanceType.Whisparr);
        var instance = new ArrInstance
        {
            Id = Guid.NewGuid(),
            Name = $"Test Whisparr v{version}",
            Url = new Uri(url),
            ApiKey = "test-api-key",
            Enabled = enabled,
            Version = version,
            ArrConfigId = arrConfig.Id,
            ArrConfig = arrConfig
        };

        arrConfig.Instances.Add(instance);
        context.ArrInstances.Add(instance);
        context.SaveChanges();

        return instance;
    }

    /// <summary>
    /// Adds an enabled download client to the context
    /// </summary>
    public static DownloadClientConfig AddDownloadClient(
        DataContext context,
        string name = "Test qBittorrent",
        DownloadClientTypeName typeName = DownloadClientTypeName.qBittorrent,
        bool enabled = true)
    {
        var config = new DownloadClientConfig
        {
            Id = Guid.NewGuid(),
            Name = name,
            TypeName = typeName,
            Type = DownloadClientType.Torrent,
            Enabled = enabled,
            Host = new Uri("http://localhost:8080"),
            Username = "admin",
            Password = "admin"
        };

        context.DownloadClients.Add(config);
        context.SaveChanges();

        return config;
    }

    /// <summary>
    /// Adds a stall rule to the context
    /// </summary>
    public static StallRule AddStallRule(
        DataContext context,
        string name = "Test Stall Rule",
        bool enabled = true,
        ushort minCompletionPercentage = 0,
        ushort maxCompletionPercentage = 100,
        int maxStrikes = 3)
    {
        var queueCleanerConfig = context.QueueCleanerConfigs.First();
        var rule = new StallRule
        {
            Id = Guid.NewGuid(),
            Name = name,
            Enabled = enabled,
            MinCompletionPercentage = minCompletionPercentage,
            MaxCompletionPercentage = maxCompletionPercentage,
            MaxStrikes = maxStrikes,
            QueueCleanerConfigId = queueCleanerConfig.Id
        };

        context.StallRules.Add(rule);
        context.SaveChanges();

        return rule;
    }

    /// <summary>
    /// Adds a slow rule to the context
    /// </summary>
    public static SlowRule AddSlowRule(
        DataContext context,
        string name = "Test Slow Rule",
        bool enabled = true,
        ushort minCompletionPercentage = 0,
        ushort maxCompletionPercentage = 100,
        int maxStrikes = 3,
        string minSpeed = "1 KB/s")
    {
        var queueCleanerConfig = context.QueueCleanerConfigs.First();
        var rule = new SlowRule
        {
            Id = Guid.NewGuid(),
            Name = name,
            Enabled = enabled,
            MinCompletionPercentage = minCompletionPercentage,
            MaxCompletionPercentage = maxCompletionPercentage,
            MaxStrikes = maxStrikes,
            MinSpeed = minSpeed,
            QueueCleanerConfigId = queueCleanerConfig.Id
        };

        context.SlowRules.Add(rule);
        context.SaveChanges();

        return rule;
    }

    /// <summary>
    /// Adds a clean category to the download cleaner config
    /// </summary>
    public static SeedingRule AddSeedingRule(
        DataContext context,
        string name = "completed",
        double maxRatio = 1.0,
        double minSeedTime = 1.0,
        double maxSeedTime = -1)
    {
        var config = context.DownloadCleanerConfigs.Include(x => x.Categories).First();
        var category = new SeedingRule
        {
            Id = Guid.NewGuid(),
            Name = name,
            MaxRatio = maxRatio,
            MinSeedTime = minSeedTime,
            MaxSeedTime = maxSeedTime,
            DeleteSourceFiles = true,
            DownloadCleanerConfigId = config.Id
        };

        config.Categories.Add(category);
        context.SeedingRules.Add(category);
        context.SaveChanges();

        return category;
    }
}
