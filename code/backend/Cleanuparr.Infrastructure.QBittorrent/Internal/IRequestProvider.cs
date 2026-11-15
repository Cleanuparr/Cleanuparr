using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using JetBrains.Annotations;

namespace QBittorrent.Client.Internal
{
    internal interface IRequestProvider
    {
        IUrlProvider Url { get; }
        
        ApiLevel ApiLevel { get; }

        (Uri url, HttpContent request) Login(string username, string password);

        (Uri url, HttpContent request) Logout();

        (Uri url, HttpContent request) AddTorrents(AddTorrentFilesRequest request);

        (Uri url, HttpContent request) AddTorrents(AddTorrentUrlsRequest request);

        (Uri url, HttpContent request) Pause(IEnumerable<string> hashes);

        (Uri url, HttpContent request) PauseAll();

        (Uri url, HttpContent request) Resume(IEnumerable<string> hashes);

        (Uri url, HttpContent request) ResumeAll();

        (Uri url, HttpContent request) AddCategory(string category, string savePath = null);

        (Uri url, HttpContent request) EditCategory(string category, string savePath);

        (Uri url, HttpContent request) DeleteCategories(IEnumerable<string> categories);

        (Uri url, HttpContent request) SetCategory(IEnumerable<string> hashes, string category);

        (Uri url, HttpContent request) GetTorrentDownloadLimit(IEnumerable<string> hashes);

        (Uri url, HttpContent request) SetTorrentDownloadLimit(IEnumerable<string> hashes, long limit);

        (Uri url, HttpContent request) GetTorrentUploadLimit(IEnumerable<string> hashes);

        (Uri url, HttpContent request) SetTorrentUploadLimit(IEnumerable<string> hashes, long limit);

        (Uri url, HttpContent request) GetGlobalDownloadLimit();

        (Uri url, HttpContent request) SetGlobalDownloadLimit(long limit);

        (Uri url, HttpContent request) GetGlobalUploadLimit();

        (Uri url, HttpContent request) SetGlobalUploadLimit(long limit);

        (Uri url, HttpContent request) MinTorrentPriority(IEnumerable<string> hashes);

        (Uri url, HttpContent request) MaxTorrentPriority(IEnumerable<string> hashes);

        (Uri url, HttpContent request) IncTorrentPriority(IEnumerable<string> hashes);

        (Uri url, HttpContent request) DecTorrentPriority(IEnumerable<string> hashes);

        (Uri url, HttpContent request) SetFilePriority(string hash, int fileId, TorrentContentPriority priority);

        (Uri url, HttpContent request) DeleteTorrents(IEnumerable<string> hashes, bool withFiles);

        (Uri url, HttpContent request) SetLocation(IEnumerable<string> hashes, string newLocation);

        (Uri url, HttpContent request) Rename(string hash, string newName);

        (Uri url, HttpContent request) AddTrackers(string hash, IEnumerable<Uri> trackers);

        (Uri url, HttpContent request) Recheck(IEnumerable<string> hashes);

        (Uri url, HttpContent request) ToggleAlternativeSpeedLimits();

        (Uri url, HttpContent request) SetAutomaticTorrentManagement(IEnumerable<string> hashes, bool enabled);

        (Uri url, HttpContent request) SetForceStart(IEnumerable<string> hashes, bool enabled);

        (Uri url, HttpContent request) SetSuperSeeding(IEnumerable<string> hashes, bool enabled);

        (Uri url, HttpContent request) ToggleFirstLastPiecePrioritized(IEnumerable<string> hashes);

        (Uri url, HttpContent request) ToggleSequentialDownload(IEnumerable<string> hashes);

        (Uri url, HttpContent request) SetPreferences(string json);

        // API 2.0+

        (Uri url, HttpContent request) AddTorrents(AddTorrentsRequest request);

        (Uri url, HttpContent request) Reannounce(IEnumerable<string> hashes);

        (Uri url, HttpContent request) EditTracker(string hash, Uri trackerUrl, Uri newTrackerUrl);

        (Uri url, HttpContent request) DeleteTrackers(string hash, IEnumerable<Uri> trackerUrls);

        (Uri url, HttpContent request) SetFilePriority(string hash, IEnumerable<int> fileIds, TorrentContentPriority priority);

        (Uri url, HttpContent request) SetShareLimits(IEnumerable<string> hashes, double ratio, TimeSpan seedingTime);

        // API 2.3

        (Uri url, HttpContent request) BanPeers(IEnumerable<string> peers);
        
        (Uri url, HttpContent request) AddTorrentPeers(IEnumerable<string> hashes, IEnumerable<string> peers);

        (Uri url, HttpContent request) CreateTags(IEnumerable<string> tags);
        
        (Uri url, HttpContent request) DeleteTags(IEnumerable<string> tags);
        
        (Uri url, HttpContent request) AddTorrentTags(IEnumerable<string> hashes, IEnumerable<string> tags);
        
        (Uri url, HttpContent request) DeleteTorrentTags(IEnumerable<string> hashes, IEnumerable<string> tags);

        // API 2.4

        (Uri url, HttpContent request) RenameFile(string hash, int fileId, string newName);

        // API 2.8

        (Uri url, HttpContent request) RenameFile(string hash, string oldPath, string newPath);

        (Uri url, HttpContent request) RenameFolder(string hash, string oldPath, string newPath);

        // API 2.9

        (Uri url, HttpContent request) SetShareLimits(IEnumerable<string> hashes, double ratio, TimeSpan seedingTime, TimeSpan inactiveSeedingTime);

        // API 2.11

        (Uri url, HttpContent request) Start(IEnumerable<string> hashes);

        (Uri url, HttpContent request) StartAll();

        (Uri url, HttpContent request) Stop(IEnumerable<string> hashes);

        (Uri url, HttpContent request) StopAll();

        // RSS

        (Uri url, HttpContent request) AddRssFolder(string path);

        (Uri url, HttpContent request) AddRssFeed(Uri url, string path);

        (Uri url, HttpContent request) DeleteRssItem(string path);

        (Uri url, HttpContent request) MoveRssItem(string path, string destinationPath);

        (Uri url, HttpContent request) SetRssAutoDownloadingRule(string name, string ruleDefinition);

        (Uri url, HttpContent request) RenameRssAutoDownloadingRule(string name, string newName);

        (Uri url, HttpContent request) DeleteRssAutoDownloadingRule(string name);

        (Uri url, HttpContent request) MarkRssItemAsRead(string itemPath, string articleId);


        // Search

        (Uri url, HttpContent request) StartSearch(string pattern, IEnumerable<string> plugins, string category);

        (Uri url, HttpContent request) StopSearch(int id);

        (Uri url, HttpContent request) DeleteSearch(int id);

        (Uri url, HttpContent request) InstallSearchPlugins(IEnumerable<Uri> sources);

        (Uri url, HttpContent request) UninstallSearchPlugins(IEnumerable<string> names);

        (Uri url, HttpContent request) EnableDisableSearchPlugins(IEnumerable<string> names, bool enable);

        (Uri url, HttpContent request) UpdateSearchPlugins();
    }
}
