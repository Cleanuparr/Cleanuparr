using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

namespace QBittorrent.Client.Internal
{
    internal sealed class Api1RequestProvider : BaseRequestProvider
    {
        internal Api1RequestProvider(Uri baseUri)
        {
            Url = new Api1UrlProvider(baseUri);
        }

        public override IUrlProvider Url { get; }

        public override ApiLevel ApiLevel => ApiLevel.V1;

        public override (Uri url, HttpContent request) Pause(IEnumerable<string> hashes)
        {
            var hashList = hashes.ToList();
            if (hashList.Count == 0)
                throw new InvalidOperationException("Exactly one hash must be provided.");

            if (hashList.Count > 1)
                throw new ApiNotSupportedException("API 1.x does not support pausing several torrents at once.", ApiLevel.V2);

            return BuildForm(Url.Pause(),
                ("hash", hashList[0]));
        }

        public override (Uri url, HttpContent request) PauseAll()
        {
            return BuildForm(Url.PauseAll());
        }

        public override (Uri url, HttpContent request) Resume(IEnumerable<string> hashes)
        {
            var hashList = hashes.ToList();
            if (hashList.Count == 0)
                throw new InvalidOperationException("Exactly one hash must be provided.");

            if (hashList.Count > 1)
                throw new ApiNotSupportedException("API 1.x does not support resuming several torrents at once.", ApiLevel.V2);

            return BuildForm(Url.Resume(),
                ("hash", hashList[0]));
        }

        public override (Uri url, HttpContent request) ResumeAll()
        {
            return BuildForm(Url.ResumeAll());
        }

        public override (Uri url, HttpContent request) EditCategory(string category, string savePath) => throw new ApiNotSupportedException(ApiLevel.V2, new Version(2, 1, 0));

        public override (Uri url, HttpContent request) DeleteTorrents(IEnumerable<string> hashes, bool withFiles)
        {
            return BuildForm(Url.DeleteTorrents(withFiles),
                ("hashes", JoinHashes(hashes)));
        }

        public override (Uri url, HttpContent request) Recheck(IEnumerable<string> hashes)
        {
            var hashList = hashes.ToList();
            if (hashList.Count == 0)
                throw new InvalidOperationException("Exactly one hash must be provided.");

            if (hashList.Count > 1)
                throw new ApiNotSupportedException("API 1.x does not support rechecking several torrents at once.", ApiLevel.V2);

            return BuildForm(Url.Recheck(),
                ("hash", hashList[0]));
        }


        public override (Uri url, HttpContent request) Reannounce(IEnumerable<string> hashes) => throw new ApiNotSupportedException(ApiLevel.V2);

        public override (Uri url, HttpContent request) EditTracker(string hash, Uri trackerUrl, Uri newTrackerUrl) => throw new ApiNotSupportedException(ApiLevel.V2, new ApiVersion(2, 2, 0));

        public override (Uri url, HttpContent request) DeleteTrackers(string hash, IEnumerable<Uri> trackerUrls) => throw new ApiNotSupportedException(ApiLevel.V2, new ApiVersion(2, 2, 0));

        public override (Uri url, HttpContent request) SetFilePriority(string hash, IEnumerable<int> fileIds, TorrentContentPriority priority) => throw new ApiNotSupportedException(ApiLevel.V2, new ApiVersion(2, 2, 0));

        public override (Uri url, HttpContent request) SetShareLimits(IEnumerable<string> hashes, double ratio, TimeSpan seedingTime) => throw new ApiNotSupportedException(ApiLevel.V2, new ApiVersion(2, 0, 1));

        public override (Uri url, HttpContent request) SetShareLimits(IEnumerable<string> hashes, double ratio, TimeSpan seedingTime, TimeSpan inactiveSeedingTime) => throw new ApiNotSupportedException(ApiLevel.V2, new ApiVersion(2, 9, 2));
        
        public override (Uri url, HttpContent request) Start(IEnumerable<string> hashes) => throw new ApiNotSupportedException(ApiLevel.V2, new ApiVersion(2, 11, 0));

        public override (Uri url, HttpContent request) StartAll() => throw new ApiNotSupportedException(ApiLevel.V2, new ApiVersion(2, 11, 0));

        public override (Uri url, HttpContent request) Stop(IEnumerable<string> hashes) => throw new ApiNotSupportedException(ApiLevel.V2, new ApiVersion(2, 11, 0));
        
        public override (Uri url, HttpContent request) StopAll() => throw new ApiNotSupportedException(ApiLevel.V2, new ApiVersion(2, 11, 0));

        public override (Uri url, HttpContent request) BanPeers(IEnumerable<string> peers) => throw new ApiNotSupportedException(ApiLevel.V2, new ApiVersion(2, 3, 0));

        public override (Uri url, HttpContent request) AddTorrentPeers(IEnumerable<string> hashes, IEnumerable<string> peers) => throw new ApiNotSupportedException(ApiLevel.V2, new ApiVersion(2, 3, 0));
        
        public override (Uri url, HttpContent request) CreateTags(IEnumerable<string> tags) => throw new ApiNotSupportedException(ApiLevel.V2, new ApiVersion(2, 3, 0));

        public override (Uri url, HttpContent request) DeleteTags(IEnumerable<string> tags) => throw new ApiNotSupportedException(ApiLevel.V2, new ApiVersion(2, 3, 0));

        public override (Uri url, HttpContent request) AddTorrentTags(IEnumerable<string> hashes, IEnumerable<string> tags) => throw new ApiNotSupportedException(ApiLevel.V2, new ApiVersion(2, 3, 0));

        public override (Uri url, HttpContent request) DeleteTorrentTags(IEnumerable<string> hashes, IEnumerable<string> tags) => throw new ApiNotSupportedException(ApiLevel.V2, new ApiVersion(2, 3, 0));

        public override (Uri url, HttpContent request) RenameFile(string hash, int fileId, string newName) => throw new ApiNotSupportedException(ApiLevel.V2, new ApiVersion(2, 4, 0));
        
        public override (Uri url, HttpContent request) RenameFile(string hash, string oldPath, string newPath) => throw new ApiNotSupportedException(ApiLevel.V2, new ApiVersion(2, 8, 0));
        
        public override (Uri url, HttpContent request) RenameFolder(string hash, string oldPath, string newPath) => throw new ApiNotSupportedException(ApiLevel.V2, new ApiVersion(2, 8, 0));

        public override (Uri url, HttpContent request) AddTorrents(AddTorrentsRequest request) => throw new ApiNotSupportedException(ApiLevel.V2);

        public override (Uri url, HttpContent request) AddRssFolder(string path) => throw new ApiNotSupportedException(ApiLevel.V2);

        public override (Uri url, HttpContent request) AddRssFeed(Uri url, string path) => throw new ApiNotSupportedException(ApiLevel.V2);

        public override (Uri url, HttpContent request) DeleteRssItem(string path) => throw new ApiNotSupportedException(ApiLevel.V2);

        public override (Uri url, HttpContent request) MoveRssItem(string path, string destinationPath) => throw new ApiNotSupportedException(ApiLevel.V2);

        public override (Uri url, HttpContent request) SetRssAutoDownloadingRule(string name, string ruleDefinition) => throw new ApiNotSupportedException(ApiLevel.V2);

        public override (Uri url, HttpContent request) RenameRssAutoDownloadingRule(string name, string newName) => throw new ApiNotSupportedException(ApiLevel.V2);

        public override (Uri url, HttpContent request) DeleteRssAutoDownloadingRule(string name) => throw new ApiNotSupportedException(ApiLevel.V2);
        
        public override (Uri url, HttpContent request) MarkRssItemAsRead(string itemPath, string articleId) => throw new ApiNotSupportedException(ApiLevel.V2, new ApiVersion(2, 5, 1));

        public override (Uri url, HttpContent request) StartSearch(string pattern, IEnumerable<string> plugins, string category) => throw new ApiNotSupportedException(ApiLevel.V2, new ApiVersion(2, 1, 1));

        public override (Uri url, HttpContent request) StopSearch(int id) => throw new ApiNotSupportedException(ApiLevel.V2, new ApiVersion(2, 1, 1));

        public override (Uri url, HttpContent request) DeleteSearch(int id) => throw new ApiNotSupportedException(ApiLevel.V2, new ApiVersion(2, 1, 1));

        public override (Uri url, HttpContent request) InstallSearchPlugins(IEnumerable<Uri> sources) => throw new ApiNotSupportedException(ApiLevel.V2, new ApiVersion(2, 1, 1));

        public override (Uri url, HttpContent request) UninstallSearchPlugins(IEnumerable<string> names) => throw new ApiNotSupportedException(ApiLevel.V2, new ApiVersion(2, 1, 1));

        public override (Uri url, HttpContent request) EnableDisableSearchPlugins(IEnumerable<string> names, bool enable) => throw new ApiNotSupportedException(ApiLevel.V2, new ApiVersion(2, 1, 1));

        public override (Uri url, HttpContent request) UpdateSearchPlugins() => throw new ApiNotSupportedException(ApiLevel.V2, new ApiVersion(2, 1, 1));
    }
}
