// Seeds a fresh, empty Cleanuparr config directory with realistic, generic-looking demo
// data (arr instances, download clients, job/seeding config, notifications, and 30 days of
// event/history data) so the app can be started and screenshotted for the docs.
//
// Usage (net10.0 file-based app, run from anywhere - the #:project path below is resolved
// relative to this file, not the current directory):
//
//   dotnet run code/backend/seed-demo-data.cs -- <configDir> [--force] [options]
//
// Refuses to run if cleanuparr.db or events.db already exist in <configDir>, unless --force
// is passed (which deletes them first, along with any -wal/-shm sidecar files). Never touches
// users.db - the account is created later through the setup API.
//
// Options (all optional, with sane defaults):
//   --force                 delete existing cleanuparr.db/events.db before seeding
//   --jobRuns=N              job runs seeded per JobType over the last 30 days (default 70)
//   --items=N                unique download items seeded, sampled without replacement from
//                            the media title pool (default 150)
//   --manual=N                extra resolved manual events, on top of the unresolved ones (default 10)
//   --cfEntries=N             max custom format score entries attempted per Sonarr/Radarr
//                             instance; actual count is capped by that instance's distinct
//                             episode/movie capacity (default 200, to saturate capacity)
//   --seekerHistory=N         additional past-cycle seeker history rows, on top of the
//                             current-cycle rows derived from each instance's capacity (default 80)
//   --searchQueue=N           pending search queue rows (default 6)
//   --trackers=N              seeker command tracker rows kept (most recent; default 10)
//
// Example:
//   cd code/backend
//   dotnet run seed-demo-data.cs -- /tmp/cleanuparr-demo --force

#:project ./Cleanuparr.Persistence.Sqlite/Cleanuparr.Persistence.Sqlite.csproj
#:property PublishAot=false

using System.Security.Cryptography;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.BlacklistSync;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Cleanuparr.Persistence.Models.Configuration.General;
using Cleanuparr.Persistence.Models.Configuration.MalwareBlocker;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Cleanuparr.Persistence.Models.Configuration.Seeker;
using Cleanuparr.Persistence.Models.Events;
using Cleanuparr.Persistence.Models.State;
using Cleanuparr.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

// ----------------------------------------------------------------------------------------
// Args
// ----------------------------------------------------------------------------------------

bool force = args.Contains("--force");
string? configDir = args.FirstOrDefault(a => !a.StartsWith("--"));

if (configDir is null)
{
    Console.Error.WriteLine("Usage: dotnet run seed-demo-data.cs -- <configDir> [--force] [options]");
    return 1;
}

int OptInt(string name, int fallback)
{
    string? raw = args.FirstOrDefault(a => a.StartsWith($"--{name}="));
    return raw is not null && int.TryParse(raw[(name.Length + 3)..], out int parsed) ? parsed : fallback;
}

int jobRunsPerType = OptInt("jobRuns", 70);
int itemCount = OptInt("items", 150);
int extraManualResolvedCount = OptInt("manual", 10);
int cfEntriesPerInstance = OptInt("cfEntries", 200);
int pastCycleHistoryCount = OptInt("seekerHistory", 80);
int searchQueueCount = OptInt("searchQueue", 6);
int trackerCount = OptInt("trackers", 10);

Directory.CreateDirectory(configDir);
string dataDbPath = Path.Combine(configDir, "cleanuparr.db");
string eventsDbPath = Path.Combine(configDir, "events.db");

bool existingDbFound = File.Exists(dataDbPath) || File.Exists(eventsDbPath);

if (existingDbFound && !force)
{
    Console.Error.WriteLine($"Refusing to run: cleanuparr.db and/or events.db already exist in {configDir}. Pass --force to delete and reseed.");
    return 1;
}

if (existingDbFound && force)
{
    foreach (string dbPath in new[] { dataDbPath, eventsDbPath })
    {
        foreach (string suffix in new[] { "", "-wal", "-shm" })
        {
            string candidate = dbPath + suffix;
            if (File.Exists(candidate))
            {
                File.Delete(candidate);
            }
        }
    }

    Console.WriteLine("Deleted existing cleanuparr.db / events.db (and sidecar files).");
}

ConfigurationPathProvider.SetConfigPath(configDir);
Random rng = Random.Shared;
DateTimeOffset now = DateTimeOffset.UtcNow;

T Pick<T>(IReadOnlyList<T> items) => items[rng.Next(items.Count)];
string Hex(int byteLength) => Convert.ToHexString(RandomNumberGenerator.GetBytes(byteLength)).ToLowerInvariant();

// Recency-biased timestamp, guaranteed <= now: most mass near `now`, a long tail back to
// `maxDays` ago. No timestamp this produces can ever land in the future.
DateTimeOffset RecentTimestamp(double maxDays = 30)
{
    double frac = Math.Pow(rng.NextDouble(), 2.2);
    double daysAgo = frac * maxDays;
    double secondsAgo = daysAgo * 86400 + rng.Next(0, 86400) * (daysAgo < 1 ? 0.999 : 1.0);
    return now.AddSeconds(-Math.Max(1, secondsAgo));
}

DateTimeOffset VeryRecentTimestamp() => now.AddMinutes(-rng.Next(1, 120));

// Builds `total` timestamps, all <= now: exactly `recentIn24h` of them fall within the last
// 24 hours (spread across the day, not clustered), the rest are recency-biased across the
// remaining `maxDays`. Used wherever a dashboard stat needs an exact, reviewable count in the
// default 24h window while still keeping the 7d/30d view consistent (a strict superset).
List<DateTimeOffset> BuildTimestamps(int total, int recentIn24h, int maxDays)
{
    recentIn24h = Math.Clamp(recentIn24h, 0, total);
    List<DateTimeOffset> result = new(total);

    for (int i = 0; i < recentIn24h; i++)
    {
        result.Add(now.AddMinutes(-(rng.NextDouble() * 1439 + 1)));
    }

    for (int i = recentIn24h; i < total; i++)
    {
        double frac = Math.Pow(rng.NextDouble(), 1.6);
        double daysAgo = 1 + frac * Math.Max(1, maxDays - 1);
        result.Add(now.AddDays(-daysAgo).AddHours(-rng.NextDouble() * 20));
    }

    return result.OrderBy(_ => rng.Next()).ToList();
}

// ==========================================================================================
// cleanuparr.db (DataContext)
// ==========================================================================================

await using DataContext data = DataContext.CreateStaticInstance();
await data.Database.MigrateAsync();
Console.WriteLine($"Migrated {dataDbPath}");

// ---- General config: update the singleton row seeded by the initial migration ----
GeneralConfig generalConfig = await data.GeneralConfigs.FirstAsync();
generalConfig.DryRun = false;
generalConfig.HttpSendUserAgent = true;
generalConfig.StatusCheckEnabled = true;
await data.SaveChangesAsync();

// ---- Arr configs: the initial migration seeds one row per InstanceType we care about ----
async Task<ArrConfig> GetArrConfig(InstanceType type)
{
    ArrConfig config = await data.ArrConfigs.FirstAsync(c => c.Type == type);
    config.FailedImportMaxStrikes = 3;
    return config;
}

ArrConfig sonarrConfig = await GetArrConfig(InstanceType.Sonarr);
ArrConfig radarrConfig = await GetArrConfig(InstanceType.Radarr);
ArrConfig lidarrConfig = await GetArrConfig(InstanceType.Lidarr);
ArrConfig readarrConfig = await GetArrConfig(InstanceType.Readarr);
await data.SaveChangesAsync();

// ---- Arr instances ----
ArrInstance MakeArrInstance(string name, Uri url, ArrConfig config) => new()
{
    Enabled = true,
    Version = 3,
    Name = name,
    Url = url,
    ApiKey = Hex(16),
    ArrConfig = config,
};

ArrInstance sonarr = MakeArrInstance("Sonarr", new Uri("http://sonarr:8989"), sonarrConfig);
ArrInstance sonarr4k = MakeArrInstance("Sonarr 4K", new Uri("http://sonarr-4k:8989"), sonarrConfig);
ArrInstance radarr = MakeArrInstance("Radarr", new Uri("http://radarr:7878"), radarrConfig);
ArrInstance radarr4k = MakeArrInstance("Radarr 4K", new Uri("http://radarr-4k:7878"), radarrConfig);
ArrInstance lidarr = MakeArrInstance("Lidarr", new Uri("http://lidarr:8686"), lidarrConfig);
ArrInstance readarr = MakeArrInstance("Readarr", new Uri("http://readarr:8787"), readarrConfig);

data.ArrInstances.AddRange(sonarr, sonarr4k, radarr, radarr4k, lidarr, readarr);
await data.SaveChangesAsync();

// ---- Download clients ----
DownloadClientConfig qbit = new()
{
    Enabled = true,
    Name = "qBittorrent",
    TypeName = DownloadClientTypeName.qBittorrent,
    Type = DownloadClientType.Torrent,
    Host = new Uri("http://qbittorrent:8080"),
    Username = "admin",
    Password = "password",
};
DownloadClientConfig transmission = new()
{
    Enabled = true,
    Name = "Transmission",
    TypeName = DownloadClientTypeName.Transmission,
    Type = DownloadClientType.Torrent,
    Host = new Uri("http://transmission:9091"),
    Username = "admin",
    Password = "password",
};
DownloadClientConfig deluge = new()
{
    Enabled = true,
    Name = "Deluge",
    TypeName = DownloadClientTypeName.Deluge,
    Type = DownloadClientType.Torrent,
    Host = new Uri("http://deluge:8112"),
    Password = "password",
};
DownloadClientConfig rtorrent = new()
{
    Enabled = true,
    Name = "rTorrent",
    TypeName = DownloadClientTypeName.rTorrent,
    Type = DownloadClientType.Torrent,
    Host = new Uri("http://rtorrent:8000"),
    UrlBase = "RPC2",
};

data.DownloadClients.AddRange(qbit, transmission, deluge, rtorrent);
await data.SaveChangesAsync();

// ---- Queue cleaner ----
QueueCleanerConfig queueCleaner = await data.QueueCleanerConfigs.FirstAsync();
queueCleaner.Enabled = true;
queueCleaner.CronExpression = "0 0/5 * * * ?";
queueCleaner.UseAdvancedScheduling = true;
queueCleaner.DownloadingMetadataMaxStrikes = 3;
queueCleaner.FailedImport = new FailedImportConfig
{
    MaxStrikes = 3,
    IgnorePrivate = true,
    PatternMode = PatternMode.Include,
    Patterns = ["password", "\\.exe$", "\\.scr$", "sample"],
    ForceImport = true,
    ForceImportMaxTries = 3,
};
await data.SaveChangesAsync();

data.StallRules.AddRange(
    new StallRule
    {
        Name = "Stalled torrents",
        Enabled = true,
        MaxStrikes = 3,
        MinCompletionPercentage = 0,
        MaxCompletionPercentage = 100,
        ResetStrikesOnProgress = true,
        MinimumProgress = "10MB",
        QueueCleanerConfigId = queueCleaner.Id,
    },
    new StallRule
    {
        Name = "Stalled metadata",
        Enabled = true,
        MaxStrikes = 3,
        MinCompletionPercentage = 0,
        MaxCompletionPercentage = 5,
        ResetStrikesOnProgress = false,
        QueueCleanerConfigId = queueCleaner.Id,
    });

data.SlowRules.AddRange(
    new SlowRule
    {
        Name = "Slow speed",
        Enabled = true,
        MaxStrikes = 3,
        ResetStrikesOnProgress = true,
        MinSpeed = "50KB",
        IgnoreAboveSize = "10GB",
        QueueCleanerConfigId = queueCleaner.Id,
    },
    new SlowRule
    {
        Name = "Slow ETA",
        Enabled = true,
        MaxStrikes = 3,
        MaxTimeHours = 12,
        QueueCleanerConfigId = queueCleaner.Id,
    });

await data.SaveChangesAsync();

// ---- Malware blocker ----
ContentBlockerConfig contentBlocker = await data.ContentBlockerConfigs.FirstAsync();
contentBlocker.Enabled = true;
contentBlocker.CronExpression = "0 */10 * * * ?";
contentBlocker.UseAdvancedScheduling = true;
contentBlocker.Sonarr = new BlocklistSettings { Enabled = true, BlocklistType = BlocklistType.Blacklist, BlocklistPath = "https://raw.githubusercontent.com/Cleanuparr/Cleanuparr/refs/heads/main/blacklist" };
contentBlocker.Radarr = new BlocklistSettings { Enabled = true, BlocklistType = BlocklistType.Blacklist, BlocklistPath = "https://raw.githubusercontent.com/Cleanuparr/Cleanuparr/refs/heads/main/blacklist" };
contentBlocker.Lidarr = new BlocklistSettings { Enabled = true, BlocklistType = BlocklistType.Blacklist, BlocklistPath = "https://raw.githubusercontent.com/Cleanuparr/Cleanuparr/refs/heads/main/blacklist" };
await data.SaveChangesAsync();

// ---- Download cleaner ----
DownloadCleanerConfig downloadCleaner = await data.DownloadCleanerConfigs.FirstAsync();
downloadCleaner.Enabled = true;
downloadCleaner.CronExpression = "0 0 * * * ?";
downloadCleaner.UseAdvancedScheduling = true;
await data.SaveChangesAsync();

data.QBitSeedingRules.AddRange(
    new QBitSeedingRule { Name = "Movies", Categories = ["radarr", "radarr-4k"], MaxRatio = 2, MaxSeedTime = 14 * 24, DownloadClientConfigId = qbit.Id },
    new QBitSeedingRule { Name = "TV", Categories = ["sonarr", "sonarr-4k"], MaxRatio = 1.5, MaxSeedTime = 10 * 24, DownloadClientConfigId = qbit.Id });
data.TransmissionSeedingRules.Add(
    new TransmissionSeedingRule { Name = "Movies", Categories = ["radarr"], MaxRatio = 2, MaxSeedTime = 14 * 24, DownloadClientConfigId = transmission.Id });
data.DelugeSeedingRules.Add(
    new DelugeSeedingRule { Name = "Movies", Categories = ["radarr"], MaxRatio = 2, MaxSeedTime = 14 * 24, DownloadClientConfigId = deluge.Id });
data.RTorrentSeedingRules.Add(
    new RTorrentSeedingRule { Name = "Movies", Categories = ["radarr"], MaxRatio = 2, MaxSeedTime = 14 * 24, DownloadClientConfigId = rtorrent.Id });

data.UnlinkedConfigs.Add(new UnlinkedConfig
{
    Enabled = true,
    TargetCategory = "cleanuparr-unlinked",
    Categories = ["radarr", "sonarr"],
    DownloadClientConfigId = qbit.Id,
});
data.OrphanedFilesConfigs.Add(new OrphanedFilesConfig
{
    Enabled = true,
    ScanDirectories = ["/downloads/complete"],
    OrphanedDirectory = "/downloads/orphaned",
    MinFileAgeHours = 24,
    DownloadClientConfigId = qbit.Id,
});
await data.SaveChangesAsync();

// ---- Seeker (global config only; per-instance configs are created further down, once the
// media pool's per-instance capacity is known, so cycle progress/duration look real) ----
SeekerConfig seeker = await data.SeekerConfigs.FirstAsync();
seeker.SearchEnabled = true;
seeker.SearchInterval = 10;
seeker.ProactiveSearchEnabled = true;
seeker.SelectionStrategy = SelectionStrategy.BalancedWeighted;
seeker.UseRoundRobin = true;
seeker.PostReleaseGraceHours = 6;
await data.SaveChangesAsync();

ArrInstance[] seekerInstances = [sonarr, sonarr4k, radarr, radarr4k];

// ---- Blacklist sync ----
BlacklistSyncConfig blacklistSync = await data.BlacklistSyncConfigs.FirstAsync();
blacklistSync.Enabled = true;
blacklistSync.CronExpression = "0 0 * * * ?";
blacklistSync.BlacklistPath = "https://raw.githubusercontent.com/Cleanuparr/Cleanuparr/refs/heads/main/blacklist";
await data.SaveChangesAsync();

// ---- Notifications ----
NotificationConfig MakeNotificationConfig(string name, NotificationProviderType type, bool enabled) => new()
{
    Name = name,
    Type = type,
    IsEnabled = enabled,
    OnFailedImportStrike = true,
    OnStalledStrike = true,
    OnQueueItemDeleted = true,
    OnDownloadCleaned = true,
    OnSearchItemGrabbed = rng.Next(2) == 0,
    OnCategoryChanged = rng.Next(2) == 0,
};

NotificationConfig discordNotification = MakeNotificationConfig("Discord", NotificationProviderType.Discord, true);
NotificationConfig telegramNotification = MakeNotificationConfig("Telegram", NotificationProviderType.Telegram, true);
NotificationConfig ntfyNotification = MakeNotificationConfig("Ntfy", NotificationProviderType.Ntfy, true);
NotificationConfig gotifyNotification = MakeNotificationConfig("Gotify", NotificationProviderType.Gotify, false);
NotificationConfig appriseNotification = MakeNotificationConfig("Apprise", NotificationProviderType.Apprise, true);
NotificationConfig pushoverNotification = MakeNotificationConfig("Pushover", NotificationProviderType.Pushover, false);

data.NotificationConfigs.AddRange(discordNotification, telegramNotification, ntfyNotification, gotifyNotification, appriseNotification, pushoverNotification);
await data.SaveChangesAsync();

data.DiscordConfigs.Add(new DiscordConfig
{
    NotificationConfigId = discordNotification.Id,
    WebhookUrl = "https://discord.com/api/webhooks/000000000000000000/xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
    Username = "Cleanuparr",
});
data.TelegramConfigs.Add(new TelegramConfig
{
    NotificationConfigId = telegramNotification.Id,
    BotToken = "123456789:ABCDefGhIJKlmNoPQRsTUVwxYZ1234567890",
    ChatId = "-1001234567890",
});
data.NtfyConfigs.Add(new NtfyConfig
{
    NotificationConfigId = ntfyNotification.Id,
    ServerUrl = "https://ntfy.sh",
    Topics = ["cleanuparr"],
    AuthenticationType = NtfyAuthenticationType.None,
    Priority = NtfyPriority.Default,
});
data.GotifyConfigs.Add(new GotifyConfig
{
    NotificationConfigId = gotifyNotification.Id,
    ServerUrl = "http://gotify",
    ApplicationToken = "A" + Hex(15),
});
data.AppriseConfigs.Add(new AppriseConfig
{
    NotificationConfigId = appriseNotification.Id,
    Mode = AppriseMode.Api,
    Url = "http://apprise:8000",
    Key = "cleanuparr",
});
data.PushoverConfigs.Add(new PushoverConfig
{
    NotificationConfigId = pushoverNotification.Id,
    ApiToken = Hex(15),
    UserKey = Hex(15),
});
await data.SaveChangesAsync();

Console.WriteLine($"Seeded {dataDbPath}: 6 arr instances, 4 download clients, queue/malware/download-cleaner rules, 6 notification providers.");

// ==========================================================================================
// events.db (EventsContext) - built from the real IDs/types/urls seeded above
// ==========================================================================================

await using EventsContext events = EventsContext.CreateStaticInstance();
await events.Database.MigrateAsync();
Console.WriteLine($"Migrated {eventsDbPath}");

string[] releaseGroups = ["GROUP", "RLSGRP", "TEAM", "DEMO", "SEED"];

List<MediaItem> mediaPool = [];
Dictionary<long, string> sonarrSeriesTitle = [];
Dictionary<long, string> radarrMovieTitle = [];

// (Title, Season, EpisodeCount, release tag). ~14 shows / ~95 episodes, alternated evenly
// between the two Sonarr instances so each has a real, non-trivial library to search through.
(string Title, int Season, int Episodes, string Tag)[] sonarrSeries =
[
    ("Severance", 2, 7, "2160p.ATVP.WEB-DL.DDP5.1.Atmos.DV.H.265"),
    ("The Bear", 3, 8, "1080p.HULU.WEB-DL.DDP5.1.H.264"),
    ("Andor", 2, 6, "2160p.DSNP.WEB-DL.DDP5.1.Atmos.DV.H.265"),
    ("Foundation", 3, 5, "2160p.APTV.WEB-DL.DDP5.1.H.265"),
    ("House of the Dragon", 2, 8, "2160p.MAX.WEB-DL.DDP5.1.Atmos.H.265"),
    ("The Last of Us", 2, 7, "2160p.MAX.WEB-DL.DDP5.1.Atmos.DV.H.265"),
    ("Fallout", 2, 6, "2160p.AMZN.WEB-DL.DDP5.1.H.265"),
    ("Shogun", 1, 9, "2160p.HULU.WEB-DL.DDP5.1.Atmos.DV.H.265"),
    ("Slow Horses", 4, 6, "1080p.APTV.WEB-DL.DDP5.1.H.264"),
    ("The Diplomat", 2, 6, "1080p.NF.WEB-DL.DDP5.1.H.264"),
    ("Silo", 2, 8, "2160p.APTV.WEB-DL.DDP5.1.Atmos.H.265"),
    ("The Penguin", 1, 7, "2160p.MAX.WEB-DL.DDP5.1.Atmos.H.265"),
    ("Ripley", 1, 8, "2160p.NF.WEB-DL.DDP5.1.H.265"),
    ("True Detective", 4, 6, "2160p.MAX.WEB-DL.DDP5.1.Atmos.H.265"),
];

long sonarrSeriesIdCounter = 1;
int sonarrAlternator = 0;
foreach ((string series, int season, int episodeCount, string tag) in sonarrSeries)
{
    ArrInstance instance = sonarrAlternator++ % 2 == 0 ? sonarr : sonarr4k;
    long seriesId = sonarrSeriesIdCounter++;
    long episodeIdCounter = seriesId * 1000;
    sonarrSeriesTitle[seriesId] = series;

    for (int ep = 1; ep <= episodeCount; ep++)
    {
        string episode = $"S{season:D2}E{ep:D2}";
        string release = $"{series.Replace(" ", ".")}.{episode}.{tag}-{Pick(releaseGroups)}";
        mediaPool.Add(new MediaItem(release, $"{series} {episode}", InstanceType.Sonarr, instance, seriesId, episodeIdCounter++, season, "2160p-WEB"));
    }
}

// ~60 movies, alternated evenly between the two Radarr instances.
(string Title, int Year, string Tag)[] radarrMovies =
[
    ("Dune: Part Two", 2024, "2160p.UHD.BluRay.REMUX.HDR.HEVC.Atmos"),
    ("Oppenheimer", 2023, "2160p.UHD.BluRay.REMUX.HDR.HEVC.TrueHD.7.1"),
    ("Poor Things", 2023, "1080p.BluRay.DD5.1.x264"),
    ("The Batman", 2022, "2160p.WEB-DL.DDP5.1.Atmos.H.265"),
    ("Blade Runner 2049", 2017, "2160p.UHD.BluRay.REMUX.HDR.HEVC.Atmos"),
    ("Civil War", 2024, "1080p.WEB-DL.DDP5.1.H.264"),
    ("Furiosa: A Mad Max Saga", 2024, "2160p.WEB-DL.DDP5.1.Atmos.H.265"),
    ("Challengers", 2024, "1080p.BluRay.DD5.1.x264"),
    ("Kinds of Kindness", 2024, "1080p.WEB-DL.DDP5.1.H.264"),
    ("The Substance", 2024, "2160p.WEB-DL.DDP5.1.Atmos.H.265"),
    ("Killers of the Flower Moon", 2023, "2160p.APTV.WEB-DL.DDP5.1.Atmos.H.265"),
    ("Barbie", 2023, "2160p.WEB-DL.DDP5.1.Atmos.H.265"),
    ("Anatomy of a Fall", 2023, "1080p.WEB-DL.DDP5.1.H.264"),
    ("The Zone of Interest", 2023, "1080p.BluRay.DD5.1.x264"),
    ("Past Lives", 2023, "1080p.WEB-DL.DDP5.1.H.264"),
    ("Godzilla Minus One", 2023, "2160p.WEB-DL.DDP5.1.Atmos.H.265"),
    ("Spider-Man: Across the Spider-Verse", 2023, "2160p.WEB-DL.DDP5.1.Atmos.H.265"),
    ("Mission: Impossible - Dead Reckoning", 2023, "2160p.WEB-DL.DDP5.1.Atmos.H.265"),
    ("Guardians of the Galaxy Vol. 3", 2023, "2160p.WEB-DL.DDP5.1.Atmos.H.265"),
    ("John Wick: Chapter 4", 2023, "2160p.BluRay.REMUX.HDR.HEVC.Atmos"),
    ("Asteroid City", 2023, "1080p.BluRay.DD5.1.x264"),
    ("Saltburn", 2023, "1080p.WEB-DL.DDP5.1.H.264"),
    ("Napoleon", 2023, "2160p.WEB-DL.DDP5.1.Atmos.H.265"),
    ("The Holdovers", 2023, "1080p.WEB-DL.DDP5.1.H.264"),
    ("Poor Things Director's Cut", 2024, "2160p.BluRay.REMUX.HDR.HEVC.DTS-HD.MA.5.1"),
    ("Deadpool & Wolverine", 2024, "2160p.WEB-DL.DDP5.1.Atmos.H.265"),
    ("Inside Out 2", 2024, "2160p.WEB-DL.DDP5.1.Atmos.H.265"),
    ("Twisters", 2024, "2160p.WEB-DL.DDP5.1.Atmos.H.265"),
    ("A Quiet Place: Day One", 2024, "1080p.WEB-DL.DDP5.1.H.264"),
    ("Alien: Romulus", 2024, "2160p.WEB-DL.DDP5.1.Atmos.H.265"),
    ("Beetlejuice Beetlejuice", 2024, "1080p.WEB-DL.DDP5.1.H.264"),
    ("Joker: Folie a Deux", 2024, "2160p.WEB-DL.DDP5.1.Atmos.H.265"),
    ("Gladiator II", 2024, "2160p.WEB-DL.DDP5.1.Atmos.H.265"),
    ("Wicked", 2024, "2160p.WEB-DL.DDP5.1.Atmos.H.265"),
    ("Moana 2", 2024, "1080p.WEB-DL.DDP5.1.H.264"),
    ("The Wild Robot", 2024, "2160p.WEB-DL.DDP5.1.Atmos.H.265"),
    ("Longlegs", 2024, "1080p.BluRay.DD5.1.x264"),
    ("Nosferatu", 2024, "2160p.WEB-DL.DDP5.1.Atmos.H.265"),
    ("The Fall Guy", 2024, "1080p.WEB-DL.DDP5.1.H.264"),
    ("Dune: Part One", 2021, "2160p.UHD.BluRay.REMUX.HDR.HEVC.Atmos"),
    ("Everything Everywhere All at Once", 2022, "1080p.BluRay.DD5.1.x264"),
    ("Top Gun: Maverick", 2022, "2160p.UHD.BluRay.REMUX.HDR.HEVC.Atmos"),
    ("Avatar: The Way of Water", 2022, "2160p.UHD.BluRay.REMUX.HDR.HEVC.Atmos"),
    ("The Banshees of Inisherin", 2022, "1080p.BluRay.DD5.1.x264"),
    ("Nope", 2022, "2160p.WEB-DL.DDP5.1.Atmos.H.265"),
    ("RRR", 2022, "1080p.WEB-DL.DDP5.1.H.264"),
    ("The Whale", 2022, "1080p.WEB-DL.DDP5.1.H.264"),
    ("Tar", 2022, "1080p.WEB-DL.DDP5.1.H.264"),
    ("Glass Onion", 2022, "2160p.WEB-DL.DDP5.1.Atmos.H.265"),
    ("No Time to Die", 2021, "2160p.UHD.BluRay.REMUX.HDR.HEVC.Atmos"),
    ("The Power of the Dog", 2021, "1080p.WEB-DL.DDP5.1.H.264"),
    ("CODA", 2021, "1080p.WEB-DL.DDP5.1.H.264"),
    ("Spider-Man: No Way Home", 2021, "2160p.UHD.BluRay.REMUX.HDR.HEVC.Atmos"),
    ("Parasite", 2019, "1080p.BluRay.DD5.1.x264"),
    ("1917", 2019, "2160p.UHD.BluRay.REMUX.HDR.HEVC.Atmos"),
    ("Knives Out", 2019, "1080p.BluRay.DD5.1.x264"),
    ("Jojo Rabbit", 2019, "1080p.BluRay.DD5.1.x264"),
    ("Joker", 2019, "2160p.UHD.BluRay.REMUX.HDR.HEVC.Atmos"),
    ("Once Upon a Time in Hollywood", 2019, "2160p.UHD.BluRay.REMUX.HDR.HEVC.Atmos"),
    ("Interstellar", 2014, "2160p.IMAX.UHD.BluRay.REMUX.HDR.HEVC.Atmos"),
];

long radarrMovieIdCounter = 1;
int radarrAlternator = 0;
foreach ((string title, int year, string tag) in radarrMovies)
{
    ArrInstance instance = radarrAlternator++ % 2 == 0 ? radarr : radarr4k;
    string cleanTitle = title.Replace(":", "").Replace(" ", ".");
    string release = $"{cleanTitle}.{year}.{tag}-{Pick(releaseGroups)}";
    long movieId = radarrMovieIdCounter++;
    radarrMovieTitle[movieId] = $"{title} ({year})";
    mediaPool.Add(new MediaItem(release, $"{title} ({year})", InstanceType.Radarr, instance, movieId, 0, 0, "2160p-BluRay"));
}

(string Artist, string Album, int Year)[] lidarrAlbums =
[
    ("Daft Punk", "Random Access Memories", 2013),
    ("Radiohead", "In Rainbows", 2007),
    ("Fleet Foxes", "Shore", 2020),
    ("Tame Impala", "The Slow Rush", 2020),
    ("Billie Eilish", "Hit Me Hard and Soft", 2024),
    ("Boygenius", "The Record", 2023),
    ("Kendrick Lamar", "Mr. Morale & the Big Steppers", 2022),
    ("SZA", "SOS", 2022),
    ("The National", "First Two Pages of Frankenstein", 2023),
    ("Sufjan Stevens", "Javelin", 2023),
];

foreach ((string artist, string album, int year) in lidarrAlbums)
{
    string release = $"{artist}-{album}-{year}-FLAC-{Pick(releaseGroups)}".Replace(" ", ".");
    mediaPool.Add(new MediaItem(release, $"{artist} - {album}", InstanceType.Lidarr, lidarr, 0, 0, 0, "Lossless"));
}

(string Author, string Book, int Year)[] readarrBooks =
[
    ("Brandon Sanderson", "The Way of Kings", 2010),
    ("N.K. Jemisin", "The Fifth Season", 2015),
    ("Andy Weir", "Project Hail Mary", 2021),
    ("Becky Chambers", "A Psalm for the Wild-Built", 2021),
    ("R.F. Kuang", "Babel", 2022),
    ("Emily St. John Mandel", "Sea of Tranquility", 2022),
    ("TJ Klune", "The House in the Cerulean Sea", 2020),
    ("Anthony Doerr", "Cloud Cuckoo Land", 2021),
    ("Hao Jingfang", "Vagabonds", 2016),
    ("Martha Wells", "Network Effect", 2020),
];

foreach ((string author, string book, int year) in readarrBooks)
{
    string release = $"{author}-{book}-{year}-EPUB-{Pick(releaseGroups)}".Replace(" ", ".");
    mediaPool.Add(new MediaItem(release, $"{author} - {book}", InstanceType.Readarr, readarr, 0, 0, 0, "Ebook"));
}

Console.WriteLine($"Built a media pool of {mediaPool.Count} distinct titles.");

// ---- Seeker per-instance configs: capacity-driven so "cycle progress" and "cycle duration"
// on the Seeker Stats page show real, non-zero values instead of "0 / N" and "-" ----
List<SeekerHistory> seekerHistory = [];
HashSet<(Guid, long, InstanceType, int, Guid)> seekerHistoryKeys = [];

// Generic filler titles for library padding: real curated titles stay scarce (they're the
// ones the docs gallery pool knows by name), so background capacity/cycle rows that exist
// only to make the counts realistic draw from plain word combinations instead.
string[] fillerAdjectives = ["Silent", "Crimson", "Hidden", "Distant", "Broken", "Golden", "Last", "Quiet", "Northern", "Lost"];
string[] fillerNouns = ["Horizon", "Static", "Harbor", "Signal", "Archive", "Current", "Ember", "Frontier", "Meridian", "Relay"];
string FillerTitle(int seed) => $"{Pick(fillerAdjectives)} {Pick(fillerNouns)} {seed}";

foreach (ArrInstance instance in seekerInstances)
{
    InstanceType instanceType = instance.ArrConfig.Type;
    bool is4k = instance.Name.Contains("4K");
    List<long> curatedIds = mediaPool
        .Where(m => m.Instance?.Id == instance.Id)
        .Select(m => m.ExternalItemId)
        .Distinct()
        .ToList();

    // Capacity models a real library rather than the small curated demo pool: Sonarr instances
    // land around 150-250 series, Radarr around 400-900 movies. 4K instances carry fewer
    // titles than their non-4K sibling, since not everything gets a 4K release.
    int capacity = instanceType switch
    {
        InstanceType.Sonarr when is4k => rng.Next(100, 150),
        InstanceType.Sonarr => rng.Next(180, 251),
        InstanceType.Radarr when is4k => rng.Next(400, 600),
        InstanceType.Radarr => rng.Next(650, 901),
        _ => Math.Max(curatedIds.Count, 1),
    };

    int cycleTarget = Math.Max(1, (int)Math.Round(capacity * (0.4 + rng.NextDouble() * 0.3))); // 40-70% through the cycle

    // Curated titles always take a cycle slot first; the remainder is padded with generic
    // filler items purely so the cycle count matches a real library's capacity.
    List<long> cycleItemIds = curatedIds.Count <= cycleTarget
        ? curatedIds
        : curatedIds.OrderBy(_ => rng.Next()).Take(cycleTarget).ToList();
    int fillerNeeded = cycleTarget - cycleItemIds.Count;

    Guid currentCycleId = Guid.NewGuid();

    // Spread the current cycle's searches over the last few days so CycleStartedAt (the
    // earliest LastSearchedAt in the cycle) is days, not seconds, before now - a real duration.
    // Curated items get the most recent slice so they sort to the top of timestamp-desc views;
    // filler items fill in behind them.
    List<DateTimeOffset> cycleTimestampsDesc = BuildTimestamps(cycleTarget, Math.Max(1, cycleTarget / 4), 6)
        .OrderByDescending(t => t)
        .ToList();
    List<DateTimeOffset> curatedTimestamps = cycleTimestampsDesc.Take(cycleItemIds.Count).OrderBy(_ => rng.Next()).ToList();
    List<DateTimeOffset> fillerTimestamps = cycleTimestampsDesc.Skip(cycleItemIds.Count).OrderBy(_ => rng.Next()).ToList();

    for (int i = 0; i < cycleItemIds.Count; i++)
    {
        long externalId = cycleItemIds[i];
        int seasonNumber = instanceType == InstanceType.Sonarr
            ? mediaPool.First(m => m.Instance?.Id == instance.Id && m.ExternalItemId == externalId).SeasonNumber
            : 0;
        string title = instanceType == InstanceType.Sonarr ? sonarrSeriesTitle[externalId] : radarrMovieTitle[externalId];
        (Guid, long, InstanceType, int, Guid) key = (instance.Id, externalId, instanceType, seasonNumber, currentCycleId);

        if (!seekerHistoryKeys.Add(key))
        {
            continue;
        }

        seekerHistory.Add(new SeekerHistory
        {
            ArrInstanceId = instance.Id,
            ExternalItemId = externalId,
            ItemType = instanceType,
            SeasonNumber = seasonNumber,
            CycleId = currentCycleId,
            LastSearchedAt = curatedTimestamps[i],
            ItemTitle = title,
            SearchCount = rng.Next(1, 4),
        });
    }

    // Filler external IDs start well above any curated series/movie ID for this instance type
    // so they can never collide with a real curated title.
    for (int f = 0; f < fillerNeeded; f++)
    {
        long fillerId = 100_000 + f;
        int seasonNumber = instanceType == InstanceType.Sonarr ? 1 : 0;
        (Guid, long, InstanceType, int, Guid) key = (instance.Id, fillerId, instanceType, seasonNumber, currentCycleId);

        if (!seekerHistoryKeys.Add(key))
        {
            continue;
        }

        seekerHistory.Add(new SeekerHistory
        {
            ArrInstanceId = instance.Id,
            ExternalItemId = fillerId,
            ItemType = instanceType,
            SeasonNumber = seasonNumber,
            CycleId = currentCycleId,
            LastSearchedAt = fillerTimestamps[f],
            ItemTitle = FillerTitle(f + 1),
            SearchCount = rng.Next(1, 4),
        });
    }

    data.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
    {
        ArrInstanceId = instance.Id,
        Enabled = true,
        UseCutoff = true,
        UseCustomFormatScore = true,
        MonitoredOnly = true,
        ActiveDownloadLimit = 3,
        MinCycleTimeDays = 7,
        TotalEligibleItems = capacity,
        CurrentCycleId = currentCycleId,
    });
}
await data.SaveChangesAsync();
Console.WriteLine($"Seeded {dataDbPath}: 4 seeker instance configs (capacity-driven cycle progress).");

// A handful of older, already-closed cycles for history depth beyond the current one.
for (int i = 0; i < pastCycleHistoryCount; i++)
{
    List<MediaItem> sonarrRadarrForHistory = mediaPool.Where(m => m.Type is InstanceType.Sonarr or InstanceType.Radarr).ToList();
    if (sonarrRadarrForHistory.Count == 0)
    {
        break;
    }

    MediaItem media = Pick(sonarrRadarrForHistory);
    Guid pastCycleId = Guid.NewGuid();
    (Guid, long, InstanceType, int, Guid) key = (media.Instance!.Id, media.ExternalItemId, media.Type!.Value, media.SeasonNumber, pastCycleId);

    if (!seekerHistoryKeys.Add(key))
    {
        continue;
    }

    string title = media.Type == InstanceType.Sonarr ? sonarrSeriesTitle[media.ExternalItemId] : radarrMovieTitle[media.ExternalItemId];
    seekerHistory.Add(new SeekerHistory
    {
        ArrInstanceId = media.Instance!.Id,
        ExternalItemId = media.ExternalItemId,
        ItemType = media.Type!.Value,
        SeasonNumber = media.SeasonNumber,
        CycleId = pastCycleId,
        LastSearchedAt = RecentTimestamp(30),
        ItemTitle = title,
        SearchCount = rng.Next(1, 6),
    });
}
events.SeekerHistory.AddRange(seekerHistory);
await events.SaveChangesAsync();

// ---- Job runs: capped, recency/diurnal-biased sample per JobType (not literal cron replay,
// which would be thousands of rows for a 5-minute job over 30 days). Failures are rare - real
// deployments mostly see Completed runs. ----
List<JobRun> jobRuns = [];
foreach (JobType jobType in Enum.GetValues<JobType>().Where(t => t != JobType.Unknown))
{
    for (int i = 0; i < jobRunsPerType; i++)
    {
        DateTimeOffset started = RecentTimestamp();
        jobRuns.Add(new JobRun
        {
            Id = Guid.CreateVersion7(),
            Type = jobType,
            StartedAt = started,
            CompletedAt = started.AddSeconds(rng.Next(2, 90)),
            Status = rng.Next(60) == 0 ? JobRunStatus.Failed : JobRunStatus.Completed,
        });
    }

    // A few very recent runs so the dashboard's "recent activity" looks alive.
    for (int i = 0; i < 3; i++)
    {
        DateTimeOffset started = VeryRecentTimestamp();
        jobRuns.Add(new JobRun
        {
            Id = Guid.CreateVersion7(),
            Type = jobType,
            StartedAt = started,
            CompletedAt = started.AddSeconds(rng.Next(2, 60)),
            Status = JobRunStatus.Completed,
        });
    }
}
events.JobRuns.AddRange(jobRuns);
await events.SaveChangesAsync();

List<JobRun> queueCleanerRuns = jobRuns.Where(j => j.Type == JobType.QueueCleaner).ToList();
List<JobRun> downloadCleanerRuns = jobRuns.Where(j => j.Type == JobType.DownloadCleaner).ToList();
List<JobRun> seekerRuns = jobRuns.Where(j => j.Type == JobType.Seeker).ToList();

// ---- Download items: sampled WITHOUT replacement so every item gets a unique release title ----
string[] importFailureReasons =
[
    "No files found are eligible for import",
    "Not a preferred word upgrade for existing episode file(s)",
    "Unable to parse file",
    "Release rejected by custom format score",
    "Sample file detected",
    "Episode file already imported at higher quality",
];
string[] categories = ["sonarr", "sonarr-4k", "radarr", "radarr-4k", "lidarr", "readarr"];

const int RemovalsTotal = 110;
const int RemovalsRecent24h = 32;
const int ResetsTotal = 35;
const int ResetsRecent24h = 10;
const int CleanedTotal = 45;
const int CleanedRecent24h = 8;
const int CategoryChangedTotal = 25;
const int CategoryChangedRecent24h = 5;
const int ForceImportedTotal = 12;
const int ForceImportedRecent24h = 3;
const int SearchTriggeredTotal = 260;
const int SearchTriggeredRecent24h = 40;

List<MediaItem> shuffledMedia = mediaPool.OrderBy(_ => rng.Next()).ToList();
int actualItemCount = Math.Min(itemCount, shuffledMedia.Count);
if (actualItemCount < itemCount)
{
    Console.WriteLine($"Note: requested {itemCount} items but the media pool only has {shuffledMedia.Count} unique titles; using {actualItemCount}.");
}

int removedTarget = Math.Min(RemovalsTotal, Math.Max(0, actualItemCount - 20));

DownloadClientConfig[] clientPool = [qbit, transmission, deluge, rtorrent];
List<SeededItem> seededItems = [];

for (int i = 0; i < actualItemCount; i++)
{
    MediaItem media = shuffledMedia[i];
    DownloadClientConfig client = Pick(clientPool);
    bool removed = i < removedTarget;
    DownloadItem item = new()
    {
        Id = Guid.CreateVersion7(),
        DownloadId = Hex(20),
        Title = media.ReleaseTitle,
        IsMarkedForRemoval = removed || rng.Next(8) == 0,
        IsRemoved = removed,
        IsReturning = removed && rng.Next(5) == 0,
    };
    seededItems.Add(new SeededItem(item, media, media.Instance, client));
}
events.DownloadItems.AddRange(seededItems.Select(s => s.Item));
await events.SaveChangesAsync();

List<SeededItem> removedItems = seededItems.Where(s => s.Item.IsRemoved).ToList();
List<SeededItem> activeItems = seededItems.Where(s => !s.Item.IsRemoved).ToList();

// ---- Strikes: climb per item; the overall timestamp distribution targets ~60-100 strike
// events in the last 24h (dashboard "Statistics" default window) while keeping 7d/30d dense. ----
StrikeType[] queueStrikeTypes = [StrikeType.Stalled, StrikeType.DownloadingMetadata, StrikeType.SlowSpeed, StrikeType.SlowTime];

List<(SeededItem Owner, StrikeType Type, int ClimbTo)> strikeAssignments = [];
foreach (SeededItem seeded in seededItems)
{
    if (rng.Next(3) == 0)
    {
        continue;
    }

    StrikeType type = rng.Next(5) == 0 ? StrikeType.DeadTorrent : Pick(queueStrikeTypes);
    int climbTo = seeded.Item.IsRemoved ? rng.Next(3, 6) : rng.Next(1, 4);
    strikeAssignments.Add((seeded, type, climbTo));
}

int strikeTotal = strikeAssignments.Sum(a => a.ClimbTo);
List<DateTimeOffset> strikeTimestamps = BuildTimestamps(strikeTotal, Math.Min(80, strikeTotal), 30);

List<(Strike Strike, SeededItem Owner)> strikes = [];
int strikeTsIndex = 0;
foreach ((SeededItem seeded, StrikeType type, int climbTo) in strikeAssignments)
{
    List<DateTimeOffset> chunk = strikeTimestamps.Skip(strikeTsIndex).Take(climbTo).OrderBy(t => t).ToList();
    strikeTsIndex += climbTo;
    JobRun ownerJob = Pick(type == StrikeType.DeadTorrent ? downloadCleanerRuns : queueCleanerRuns);

    foreach (DateTimeOffset createdAt in chunk)
    {
        Strike strike = new()
        {
            Id = Guid.CreateVersion7(),
            DownloadItemId = seeded.Item.Id,
            JobRunId = ownerJob.Id,
            Type = type,
            CreatedAt = createdAt,
            LastDownloadedBytes = type is StrikeType.Stalled or StrikeType.DownloadingMetadata
                ? (long)rng.Next(0, 500_000) * 1024
                : null,
        };
        strikes.Add((strike, seeded));
    }
}
events.Strikes.AddRange(strikes.Select(s => s.Strike));
await events.SaveChangesAsync();

// ---- App events: mirror the exact message formats the real EventPublisher writes ----
List<AppEvent> appEvents = [];

void AddAppEvent(AppEvent appEvent)
{
    appEvent.Id = Guid.CreateVersion7();
    appEvents.Add(appEvent);
}

foreach ((Strike strike, SeededItem owner) in strikes)
{
    EventType eventType = strike.Type switch
    {
        StrikeType.Stalled => EventType.StalledStrike,
        StrikeType.DownloadingMetadata => EventType.DownloadingMetadataStrike,
        StrikeType.FailedImport => EventType.FailedImportStrike,
        StrikeType.SlowSpeed => EventType.SlowSpeedStrike,
        StrikeType.SlowTime => EventType.SlowTimeStrike,
        StrikeType.DeadTorrent => EventType.DeadTorrentStrike,
        _ => EventType.StalledStrike,
    };
    int strikeCount = strikes.Count(s => s.Owner.Item.Id == owner.Item.Id && s.Strike.Type == strike.Type && s.Strike.CreatedAt <= strike.CreatedAt);

    AddAppEvent(new AppEvent
    {
        Timestamp = strike.CreatedAt,
        EventType = eventType,
        Severity = EventSeverity.Important,
        Message = $"Item '{owner.Media.DisplayTitle}' has been struck {strikeCount} times for reason '{strike.Type}'",
        StrikeId = strike.Id,
        JobRunId = strike.JobRunId,
        ArrInstanceId = eventType != EventType.DeadTorrentStrike ? owner.Instance?.Id : null,
        DownloadClientId = owner.Client.Id,
        ItemTitle = owner.Media.DisplayTitle,
        ItemHash = owner.Item.DownloadId,
        StrikeCount = strikeCount,
    });
}

// A handful of failed-import strikes (distinct event type, carries failure reasons).
List<SeededItem> failedImportOwners = seededItems.OrderBy(_ => rng.Next()).Take(25).ToList();
List<DateTimeOffset> failedImportTimestamps = BuildTimestamps(failedImportOwners.Count, 6, 20);
for (int i = 0; i < failedImportOwners.Count; i++)
{
    SeededItem seeded = failedImportOwners[i];
    int strikeCount = rng.Next(1, 4);
    JobRun job = Pick(queueCleanerRuns);
    List<string> reasons = [Pick(importFailureReasons), Pick(importFailureReasons)];

    AddAppEvent(new AppEvent
    {
        Timestamp = failedImportTimestamps[i],
        EventType = EventType.FailedImportStrike,
        Severity = EventSeverity.Important,
        Message = $"Item '{seeded.Media.DisplayTitle}' has been struck {strikeCount} times for reason 'FailedImport'",
        JobRunId = job.Id,
        ArrInstanceId = seeded.Instance?.Id,
        DownloadClientId = seeded.Client.Id,
        ItemTitle = seeded.Media.DisplayTitle,
        ItemHash = seeded.Item.DownloadId,
        StrikeCount = strikeCount,
        FailedImportReasons = reasons,
    });
}

// Strike resets for items that recovered (targets ~10 in the last 24h, ~35 total).
List<DateTimeOffset> resetTimestamps = BuildTimestamps(ResetsTotal, ResetsRecent24h, 25);
for (int i = 0; i < resetTimestamps.Count; i++)
{
    SeededItem seeded = activeItems.Count > 0 ? Pick(activeItems) : Pick(seededItems);
    int strikeCount = rng.Next(1, 4);
    StrikeType type = Pick(queueStrikeTypes);

    AddAppEvent(new AppEvent
    {
        Timestamp = resetTimestamps[i],
        EventType = EventType.StrikeReset,
        Severity = EventSeverity.Information,
        Message = $"'{seeded.Media.DisplayTitle}' recovered — {strikeCount} '{type}' strike(s) reset",
        JobRunId = Pick(queueCleanerRuns).Id,
        ArrInstanceId = seeded.Instance?.Id,
        DownloadClientId = seeded.Client.Id,
        ItemTitle = seeded.Media.DisplayTitle,
        ItemHash = seeded.Item.DownloadId,
        StrikeCount = 0,
    });
}

// Removals (targets ~32 in the last 24h, ~110 total). About a quarter carry a malware delete
// reason, so the dashboard's "malware blocked" card and timeline both move.
DeleteReason[] malwareReasons = [DeleteReason.AllFilesBlocked, DeleteReason.AtLeastOneFileBlocked];
DeleteReason[] otherDeleteReasons = [DeleteReason.Stalled, DeleteReason.FailedImport, DeleteReason.SlowSpeed, DeleteReason.SlowTime, DeleteReason.AllFilesSkipped];
List<DateTimeOffset> removalTimestamps = BuildTimestamps(removedItems.Count, RemovalsRecent24h, 30);

for (int i = 0; i < removedItems.Count; i++)
{
    SeededItem seeded = removedItems[i];
    DeleteReason reason = i % 4 == 0 ? Pick(malwareReasons) : Pick(otherDeleteReasons);
    bool removeFromClient = rng.Next(5) != 0;
    DateTimeOffset markedAt = removalTimestamps[i];

    AddAppEvent(new AppEvent
    {
        Timestamp = markedAt,
        EventType = EventType.DownloadMarkedForDeletion,
        Severity = EventSeverity.Important,
        Message = "Download marked for deletion",
        JobRunId = Pick(queueCleanerRuns).Id,
        ArrInstanceId = seeded.Instance?.Id,
        DownloadClientId = seeded.Client.Id,
        ItemTitle = seeded.Media.DisplayTitle,
        ItemHash = seeded.Item.DownloadId,
    });

    AddAppEvent(new AppEvent
    {
        Timestamp = markedAt.AddSeconds(rng.Next(1, 20)),
        EventType = EventType.QueueItemDeleted,
        Severity = EventSeverity.Important,
        Message = $"Deleting item from queue with reason: {reason}",
        JobRunId = Pick(queueCleanerRuns).Id,
        ArrInstanceId = seeded.Instance?.Id,
        DownloadClientId = seeded.Client.Id,
        ItemTitle = seeded.Media.DisplayTitle,
        ItemHash = seeded.Item.DownloadId,
        DeleteReason = reason,
        RemoveFromClient = removeFromClient,
    });
}

CleanReason[] cleanReasons = [CleanReason.MaxRatioReached, CleanReason.MaxSeedTimeReached];
List<SeededItem> cleanedOwners = seededItems.OrderBy(_ => rng.Next()).Take(CleanedTotal).ToList();
List<DateTimeOffset> cleanedTimestamps = BuildTimestamps(cleanedOwners.Count, CleanedRecent24h, 25);

for (int i = 0; i < cleanedOwners.Count; i++)
{
    SeededItem seeded = cleanedOwners[i];
    CleanReason reason = Pick(cleanReasons);
    double ratio = Math.Round(rng.NextDouble() * 4 + 0.5, 2);
    double seedHours = Math.Round(rng.NextDouble() * 300 + 24, 1);
    bool stoppedOnly = rng.Next(4) == 0;

    AddAppEvent(new AppEvent
    {
        Timestamp = cleanedTimestamps[i],
        EventType = stoppedOnly ? EventType.DownloadStopped : EventType.DownloadCleaned,
        Severity = EventSeverity.Important,
        Message = stoppedOnly
            ? $"Stopped item in download client with reason: {reason}"
            : $"Cleaned item from download client with reason: {reason}",
        JobRunId = Pick(downloadCleanerRuns).Id,
        DownloadClientId = seeded.Client.Id,
        ItemTitle = seeded.Media.DisplayTitle,
        ItemHash = seeded.Item.DownloadId,
        CleanReason = reason,
        CleanedCategory = Pick(categories),
        SeedRatio = ratio,
        SeedingTimeHours = seedHours,
    });
}

List<SeededItem> categoryChangedOwners = seededItems.OrderBy(_ => rng.Next()).Take(CategoryChangedTotal).ToList();
List<DateTimeOffset> categoryChangedTimestamps = BuildTimestamps(categoryChangedOwners.Count, CategoryChangedRecent24h, 20);

for (int i = 0; i < categoryChangedOwners.Count; i++)
{
    SeededItem seeded = categoryChangedOwners[i];
    bool isTag = rng.Next(2) == 0;
    string oldCategory = Pick(categories);
    string newCategory = isTag ? "cleanuparr-unlinked" : Pick(categories);

    AddAppEvent(new AppEvent
    {
        Timestamp = categoryChangedTimestamps[i],
        EventType = EventType.CategoryChanged,
        Severity = EventSeverity.Information,
        Message = isTag ? $"Tag '{newCategory}' added to download" : $"Category changed from '{oldCategory}' to '{newCategory}'",
        JobRunId = Pick(downloadCleanerRuns).Id,
        DownloadClientId = seeded.Client.Id,
        ItemTitle = seeded.Media.DisplayTitle,
        ItemHash = seeded.Item.DownloadId,
        OldCategory = oldCategory,
        NewCategory = newCategory,
        IsCategoryTag = isTag,
    });
}

List<SeededItem> forceImportedOwners = seededItems.OrderBy(_ => rng.Next()).Take(ForceImportedTotal).ToList();
List<DateTimeOffset> forceImportedTimestamps = BuildTimestamps(forceImportedOwners.Count, ForceImportedRecent24h, 15);

for (int i = 0; i < forceImportedOwners.Count; i++)
{
    SeededItem seeded = forceImportedOwners[i];
    AddAppEvent(new AppEvent
    {
        Timestamp = forceImportedTimestamps[i],
        EventType = EventType.ForceImported,
        Severity = EventSeverity.Important,
        Message = "Imported a download the arr had blocked",
        JobRunId = Pick(queueCleanerRuns).Id,
        ArrInstanceId = seeded.Instance?.Id,
        DownloadClientId = seeded.Client.Id,
        ItemTitle = seeded.Media.DisplayTitle,
        ItemHash = seeded.Item.DownloadId,
    });
}

// ---- Search-triggered events + seeker command trackers (linked, like SeekerCommandMonitor).
// Total is capped independently of the media pool size so it can't dominate the 24h "Event
// activity" chart just because the pool grew for CF-score coverage. ----
List<SeekerCommandTracker> trackers = [];
List<MediaItem> sonarrRadarrMedia = mediaPool.Where(m => m.Type is InstanceType.Sonarr or InstanceType.Radarr).ToList();
List<DateTimeOffset> searchTimestamps = BuildTimestamps(SearchTriggeredTotal, SearchTriggeredRecent24h, 30);

for (int i = 0; i < searchTimestamps.Count && sonarrRadarrMedia.Count > 0; i++)
{
    MediaItem media = Pick(sonarrRadarrMedia);
    DateTimeOffset triggeredAt = searchTimestamps[i];
    SeekerSearchType searchType = rng.Next(3) == 0 ? SeekerSearchType.Replacement : SeekerSearchType.Proactive;
    SeekerSearchReason searchReason = searchType == SeekerSearchType.Replacement
        ? SeekerSearchReason.Replacement
        : Pick(new[] { SeekerSearchReason.Missing, SeekerSearchReason.QualityCutoffNotMet, SeekerSearchReason.CustomFormatScoreBelowCutoff });
    bool completed = rng.Next(6) != 0;
    SearchCommandStatus status = completed ? SearchCommandStatus.Completed : Pick(new[] { SearchCommandStatus.Pending, SearchCommandStatus.Started, SearchCommandStatus.Failed });
    List<string> grabbed = completed && rng.Next(2) == 0 ? [media.ReleaseTitle] : [];

    AppEvent searchEvent = new()
    {
        Id = Guid.CreateVersion7(),
        Timestamp = triggeredAt,
        EventType = EventType.SearchTriggered,
        Severity = EventSeverity.Information,
        Message = $"Search triggered for {media.DisplayTitle}",
        JobRunId = Pick(seekerRuns).Id,
        ArrInstanceId = media.Instance?.Id,
        SearchStatus = status,
        CompletedAt = status is SearchCommandStatus.Completed or SearchCommandStatus.Failed ? triggeredAt.AddMinutes(rng.Next(1, 30)) : null,
        CycleId = Guid.NewGuid(),
        ItemTitle = media.DisplayTitle,
        SearchType = searchType,
        SearchReason = searchReason,
        GrabbedItems = grabbed,
    };
    appEvents.Add(searchEvent);

    trackers.Add(new SeekerCommandTracker
    {
        ArrInstanceId = media.Instance!.Id,
        CommandId = rng.Next(1, 1_000_000),
        EventId = searchEvent.Id,
        ExternalItemId = media.ExternalItemId,
        EpisodeId = media.EpisodeId,
        ItemTitle = media.DisplayTitle,
        SeasonNumber = media.SeasonNumber,
        CreatedAt = triggeredAt,
        Status = status,
    });
}

// ---- Back every SeekerHistory search with a SearchTriggered event, so the per-instance
// "Searches" cards (SUM of SeekerHistory.SearchCount) can never outrun the Total/7d/30d counts
// the same Searches page shows above them. Always > 24h old so the dashboard's 24h stats and
// the Events page 24h chart (both windowed to the last day) don't move. ----
DateTimeOffset BackingSearchTimestamp(double maxDays = 35)
{
    double frac = Math.Pow(rng.NextDouble(), 2.2);
    double daysAgo = 1.05 + frac * (maxDays - 1.05); // 1.05 days keeps a safety margin over the 24h cutoff
    return now.AddDays(-daysAgo).AddSeconds(-rng.Next(0, 86400));
}

foreach (SeekerHistory history in seekerHistory)
{
    for (int s = 0; s < history.SearchCount; s++)
    {
        DateTimeOffset backedAt = BackingSearchTimestamp();
        SeekerSearchType searchType = rng.Next(3) == 0 ? SeekerSearchType.Replacement : SeekerSearchType.Proactive;
        SeekerSearchReason searchReason = searchType == SeekerSearchType.Replacement
            ? SeekerSearchReason.Replacement
            : Pick(new[] { SeekerSearchReason.Missing, SeekerSearchReason.QualityCutoffNotMet, SeekerSearchReason.CustomFormatScoreBelowCutoff });
        bool completed = rng.Next(6) != 0;
        SearchCommandStatus status = completed ? SearchCommandStatus.Completed : Pick(new[] { SearchCommandStatus.Pending, SearchCommandStatus.Started, SearchCommandStatus.Failed });
        List<string> grabbed = completed && rng.Next(2) == 0 ? [history.ItemTitle] : [];

        AddAppEvent(new AppEvent
        {
            Timestamp = backedAt,
            EventType = EventType.SearchTriggered,
            Severity = EventSeverity.Information,
            Message = $"Search triggered for {history.ItemTitle}",
            JobRunId = Pick(seekerRuns).Id,
            ArrInstanceId = history.ArrInstanceId,
            SearchStatus = status,
            CompletedAt = status is SearchCommandStatus.Completed or SearchCommandStatus.Failed ? backedAt.AddMinutes(rng.Next(1, 30)) : null,
            CycleId = history.CycleId,
            ItemTitle = history.ItemTitle,
            SearchType = searchType,
            SearchReason = searchReason,
            GrabbedItems = grabbed,
        });
    }
}

events.Events.AddRange(appEvents);
await events.SaveChangesAsync();

trackers = trackers.OrderByDescending(t => t.CreatedAt).Take(trackerCount).ToList();
events.SeekerCommandTrackers.AddRange(trackers);
await events.SaveChangesAsync();

// ---- Manual events: unresolved (shown on the dashboard) + resolved history ----
List<ManualEvent> manualEvents = [];
List<SeededItem> recurringCandidates = seededItems.Where(s => s.Item.IsReturning).ToList();
if (recurringCandidates.Count == 0)
{
    recurringCandidates = seededItems.Take(5).ToList();
}

int unresolvedTarget = Math.Clamp(recurringCandidates.Count + 4, 8, 12);
for (int i = 0; i < unresolvedTarget; i++)
{
    SeededItem seeded = seededItems[i % seededItems.Count];
    bool recurring = i % 2 == 0;

    manualEvents.Add(new ManualEvent
    {
        Id = Guid.CreateVersion7(),
        Timestamp = RecentTimestamp(7),
        Severity = recurring ? EventSeverity.Important : EventSeverity.Warning,
        Type = recurring ? ManualEventType.RecurringDownload : ManualEventType.SearchNotTriggered,
        Message = recurring
            ? "Download keeps coming back after deletion\nTo prevent further issues, please consult the prerequisites: https://cleanuparr.github.io/Cleanuparr/docs/installation/"
            : "Replacement search was not triggered after removal\nPlease trigger a manual search if needed",
        ItemTitle = seeded.Media.DisplayTitle,
        ItemHash = Hex(20),
        StrikeCount = recurring ? rng.Next(2, 6) : null,
        IsResolved = false,
        JobRunId = Pick(queueCleanerRuns).Id,
        InstanceType = seeded.Instance?.ArrConfig.Type,
        InstanceUrl = seeded.Instance?.Url.ToString(),
        DownloadClientType = seeded.Client.TypeName,
        DownloadClientName = seeded.Client.Name,
    });
}

for (int i = 0; i < extraManualResolvedCount; i++)
{
    SeededItem seeded = Pick(seededItems);
    bool recurring = rng.Next(2) == 0;
    DateTimeOffset createdAt = RecentTimestamp(25);

    manualEvents.Add(new ManualEvent
    {
        Id = Guid.CreateVersion7(),
        Timestamp = createdAt,
        Severity = recurring ? EventSeverity.Important : EventSeverity.Warning,
        Type = recurring ? ManualEventType.RecurringDownload : ManualEventType.SearchNotTriggered,
        Message = recurring
            ? "Download keeps coming back after deletion\nTo prevent further issues, please consult the prerequisites: https://cleanuparr.github.io/Cleanuparr/docs/installation/"
            : "Replacement search was not triggered after removal\nPlease trigger a manual search if needed",
        ItemTitle = seeded.Media.DisplayTitle,
        ItemHash = Hex(20),
        StrikeCount = recurring ? rng.Next(2, 6) : null,
        IsResolved = true,
        ResolvedAt = createdAt.AddHours(rng.Next(1, 48)),
        JobRunId = Pick(queueCleanerRuns).Id,
        InstanceType = seeded.Instance?.ArrConfig.Type,
        InstanceUrl = seeded.Instance?.Url.ToString(),
        DownloadClientType = seeded.Client.TypeName,
        DownloadClientName = seeded.Client.Name,
    });
}

events.ManualEvents.AddRange(manualEvents);
await events.SaveChangesAsync();

// ---- Search queue ----
for (int i = 0; i < searchQueueCount && sonarrRadarrMedia.Count > 0; i++)
{
    MediaItem media = Pick(sonarrRadarrMedia);
    events.SearchQueue.Add(new SearchQueueItem
    {
        ArrInstanceId = media.Instance!.Id,
        ItemId = media.Type == InstanceType.Sonarr ? media.EpisodeId : media.ExternalItemId,
        SeriesId = media.Type == InstanceType.Sonarr ? media.ExternalItemId : null,
        SearchType = media.Type == InstanceType.Sonarr ? "Episode" : null,
        Title = media.DisplayTitle,
        CreatedAt = VeryRecentTimestamp(),
    });
}
await events.SaveChangesAsync();

// ---- Custom format score entries + history. Entries are capped by each instance's distinct
// episode/movie capacity (dedup key is instance+item+episode), so the grown media pool above
// is what gets this to ~150-200 total across the 4 instances. Upgraded entries get BOTH their
// baseline and upgraded history rows inside the last 7 days, since that's what the CF stats
// endpoint's "recent upgrades" counter requires (it only compares consecutive rows that are
// both within that window). ----
HashSet<(Guid, long, long)> cfEntryKeys = [];
List<CustomFormatScoreEntry> cfEntries = [];
List<CustomFormatScoreHistory> cfHistory = [];
string[] qualityProfiles = ["HD-1080p", "UHD-2160p", "Remux-1080p", "Any"];

foreach (ArrInstance instance in seekerInstances)
{
    InstanceType instanceType = instance.ArrConfig.Type;
    List<MediaItem> instanceMedia = mediaPool.Where(m => m.Instance?.Id == instance.Id).ToList();
    if (instanceMedia.Count == 0)
    {
        continue;
    }

    for (int i = 0; i < cfEntriesPerInstance; i++)
    {
        MediaItem media = Pick(instanceMedia);
        (Guid, long, long) key = (instance.Id, media.ExternalItemId, media.EpisodeId);
        if (!cfEntryKeys.Add(key))
        {
            continue;
        }

        int cutoff = Pick(new[] { 0, 100, 200 });
        bool upgraded = rng.Next(3) == 0;
        int startScore = rng.Next(-50, cutoff + 1);
        int currentScore = upgraded ? Math.Min(cutoff + rng.Next(0, 80), 400) : startScore;

        DateTimeOffset? lastUpgradedAt = null;
        DateTimeOffset historyStart;

        if (upgraded)
        {
            // Both rows must land within the last 7 days for the "recent upgrades" stat to count them.
            DateTimeOffset baseline = now.AddDays(-rng.Next(3, 7)).AddHours(-rng.NextDouble() * 12);
            DateTimeOffset upgrade = baseline.AddDays(rng.Next(1, 3)).AddHours(rng.NextDouble() * 6);
            if (upgrade > now)
            {
                upgrade = now.AddHours(-rng.NextDouble() * 4);
            }

            historyStart = baseline;
            lastUpgradedAt = upgrade;
        }
        else
        {
            historyStart = RecentTimestamp(60);
        }

        cfEntries.Add(new CustomFormatScoreEntry
        {
            ArrInstanceId = instance.Id,
            ExternalItemId = media.ExternalItemId,
            EpisodeId = media.EpisodeId,
            ItemType = instanceType,
            Title = media.DisplayTitle,
            FileId = 500_000 + i,
            CurrentScore = currentScore,
            CutoffScore = cutoff,
            QualityProfileName = Pick(qualityProfiles),
            IsMonitored = rng.Next(6) != 0,
            LastSyncedAt = RecentTimestamp(2),
            LastUpgradedAt = lastUpgradedAt,
        });

        cfHistory.Add(new CustomFormatScoreHistory
        {
            ArrInstanceId = instance.Id,
            ExternalItemId = media.ExternalItemId,
            EpisodeId = media.EpisodeId,
            ItemType = instanceType,
            Title = media.DisplayTitle,
            Score = startScore,
            CutoffScore = cutoff,
            RecordedAt = historyStart,
        });

        if (upgraded)
        {
            cfHistory.Add(new CustomFormatScoreHistory
            {
                ArrInstanceId = instance.Id,
                ExternalItemId = media.ExternalItemId,
                EpisodeId = media.EpisodeId,
                ItemType = instanceType,
                Title = media.DisplayTitle,
                Score = currentScore,
                CutoffScore = cutoff,
                RecordedAt = lastUpgradedAt!.Value,
            });
        }
    }
}
events.CustomFormatScoreEntries.AddRange(cfEntries);
events.CustomFormatScoreHistory.AddRange(cfHistory);
await events.SaveChangesAsync();

Console.WriteLine(
    $"Seeded {eventsDbPath}: {jobRuns.Count} job runs, {seededItems.Count} download items ({removedItems.Count} removed), " +
    $"{strikes.Count} strikes, {appEvents.Count} app events, {manualEvents.Count} manual events, {seekerHistory.Count} seeker history rows, " +
    $"{searchQueueCount} search queue rows, {trackers.Count} command trackers, {cfEntries.Count} CF score entries, {cfHistory.Count} CF score history rows.");

Console.WriteLine("Done.");
return 0;

readonly record struct MediaItem(
    string ReleaseTitle,
    string DisplayTitle,
    InstanceType? Type,
    ArrInstance? Instance,
    long ExternalItemId,
    long EpisodeId,
    int SeasonNumber,
    string QualityProfile);

readonly record struct SeededItem(DownloadItem Item, MediaItem Media, ArrInstance? Instance, DownloadClientConfig Client);
