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

namespace Cleanuparr.Api.Tests.Features.DownloadCleaner.TestHelpers;

/// <summary>
/// Factory for creating SQLite in-memory contexts for SeedingRulesController tests
/// </summary>
public static class SeedingRulesTestDataFactory
{
    public static DataContext CreateDataContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<DataContext>()
            .UseSqlite(connection)
            .Options;

        var context = new DataContext(options);
        context.Database.EnsureCreated();

        SeedDefaultData(context);
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

    public static DownloadClientConfig AddDownloadClient(
        DataContext context,
        DownloadClientTypeName typeName = DownloadClientTypeName.qBittorrent,
        string name = "Test qBittorrent")
    {
        var config = new DownloadClientConfig
        {
            Id = Guid.NewGuid(),
            Name = name,
            TypeName = typeName,
            Type = DownloadClientType.Torrent,
            Enabled = true,
            Host = new Uri("http://localhost:8080"),
            Username = "admin",
            Password = "admin"
        };

        context.DownloadClients.Add(config);
        context.SaveChanges();
        return config;
    }

    public static QBitSeedingRule AddQBitSeedingRule(
        DataContext context,
        Guid downloadClientId,
        string name = "Test Rule",
        int priority = 1,
        List<string>? categories = null,
        List<string>? trackerPatterns = null,
        List<string>? tagsAny = null,
        List<string>? tagsAll = null,
        double maxRatio = 2.0,
        double minSeedTime = 0,
        double maxSeedTime = -1)
    {
        var rule = new QBitSeedingRule
        {
            Id = Guid.NewGuid(),
            DownloadClientConfigId = downloadClientId,
            Name = name,
            Priority = priority,
            Categories = categories ?? ["movies"],
            TrackerPatterns = trackerPatterns ?? [],
            TagsAny = tagsAny ?? [],
            TagsAll = tagsAll ?? [],
            PrivacyType = TorrentPrivacyType.Both,
            MaxRatio = maxRatio,
            MinSeedTime = minSeedTime,
            MaxSeedTime = maxSeedTime,
            DeleteSourceFiles = true,
        };

        context.QBitSeedingRules.Add(rule);
        context.SaveChanges();
        return rule;
    }

    public static DelugeSeedingRule AddDelugeSeedingRule(
        DataContext context,
        Guid downloadClientId,
        string name = "Test Rule",
        int priority = 1,
        List<string>? categories = null,
        double maxRatio = 2.0,
        double maxSeedTime = -1)
    {
        var rule = new DelugeSeedingRule
        {
            Id = Guid.NewGuid(),
            DownloadClientConfigId = downloadClientId,
            Name = name,
            Priority = priority,
            Categories = categories ?? ["movies"],
            TrackerPatterns = [],
            PrivacyType = TorrentPrivacyType.Both,
            MaxRatio = maxRatio,
            MinSeedTime = 0,
            MaxSeedTime = maxSeedTime,
            DeleteSourceFiles = true,
        };

        context.DelugeSeedingRules.Add(rule);
        context.SaveChanges();
        return rule;
    }

    public static TransmissionSeedingRule AddTransmissionSeedingRule(
        DataContext context,
        Guid downloadClientId,
        string name = "Test Rule",
        int priority = 1,
        List<string>? categories = null,
        double maxRatio = 2.0,
        double maxSeedTime = -1)
    {
        var rule = new TransmissionSeedingRule
        {
            Id = Guid.NewGuid(),
            DownloadClientConfigId = downloadClientId,
            Name = name,
            Priority = priority,
            Categories = categories ?? ["movies"],
            TrackerPatterns = [],
            TagsAny = [],
            TagsAll = [],
            PrivacyType = TorrentPrivacyType.Both,
            MaxRatio = maxRatio,
            MinSeedTime = 0,
            MaxSeedTime = maxSeedTime,
            DeleteSourceFiles = true,
        };

        context.TransmissionSeedingRules.Add(rule);
        context.SaveChanges();
        return rule;
    }
}
