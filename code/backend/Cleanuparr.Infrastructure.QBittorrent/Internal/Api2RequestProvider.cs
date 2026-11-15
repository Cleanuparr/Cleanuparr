using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using QBittorrent.Client.Extensions;

namespace QBittorrent.Client.Internal
{
    internal sealed class Api2RequestProvider : BaseRequestProvider
    {
        internal Api2RequestProvider(Uri baseUri)
        {
            Url = new Api2UrlProvider(baseUri);
        }

        public override IUrlProvider Url { get; }

        public override ApiLevel ApiLevel => ApiLevel.V2;

        public override (Uri url, HttpContent request) Pause(IEnumerable<string> hashes)
        {
            return BuildForm(Url.Pause(),
                ("hashes", JoinHashes(hashes)));
        }

        public override (Uri url, HttpContent request) PauseAll()
        {
            return BuildForm(Url.PauseAll(),
                ("hashes", "all"));
        }

        public override (Uri url, HttpContent request) Resume(IEnumerable<string> hashes)
        {
            return BuildForm(Url.Resume(),
                ("hashes", JoinHashes(hashes)));
        }

        public override (Uri url, HttpContent request) ResumeAll()
        {
            return BuildForm(Url.ResumeAll(),
                ("hashes", "all"));
        }

        public override (Uri url, HttpContent request) EditCategory(string category, string savePath)
        {
            return BuildForm(Url.EditCategory(),
                ("category", category),
                ("savePath", savePath));
        }

        public override (Uri url, HttpContent request) DeleteTorrents(IEnumerable<string> hashes, bool withFiles)
        {
            return BuildForm(Url.DeleteTorrents(withFiles),
                ("hashes", JoinHashes(hashes)),
                ("deleteFiles", withFiles.ToLowerString()));
        }

        public override (Uri url, HttpContent request) Recheck(IEnumerable<string> hashes)
        {
            return BuildForm(Url.Recheck(),
                ("hashes", JoinHashes(hashes)));
        }

        public override (Uri url, HttpContent request) Reannounce(IEnumerable<string> hashes)
        {
            return BuildForm(Url.Reannounce(),
                ("hashes", JoinHashes(hashes)));
        }

        public override (Uri url, HttpContent request) EditTracker(string hash, Uri trackerUrl, Uri newTrackerUrl)
        {
            return BuildForm(Url.EditTracker(),
                ("hash", hash),
                ("origUrl", trackerUrl.AbsoluteUri),
                ("newUrl", newTrackerUrl.AbsoluteUri));
        }

        public override (Uri url, HttpContent request) DeleteTrackers(string hash, IEnumerable<Uri> trackerUrls)
        {
            return BuildForm(Url.DeleteTrackers(),
                ("hash", hash),
                ("urls", string.Join("|", trackerUrls.Select(u => u.AbsoluteUri))));
        }

        public override (Uri url, HttpContent request) SetFilePriority(string hash, IEnumerable<int> fileIds, TorrentContentPriority priority)
        {
            return BuildForm(Url.SetFilePriority(),
                ("hash", hash),
                ("id", string.Join("|", fileIds)),
                ("priority", priority.ToString("D")));
        }

        public override (Uri url, HttpContent request) SetShareLimits(IEnumerable<string> hashes, double ratio, TimeSpan seedingTime)
        {
            return BuildForm(Url.SetShareLimits(),
                ("hashes", JoinHashes(hashes)),
                ("ratioLimit", ratio.ToString("R", CultureInfo.InvariantCulture)),
                ("seedingTimeLimit", seedingTime.TotalMinutes.ToString("F0")));
        }

        public override (Uri url, HttpContent request) SetShareLimits(IEnumerable<string> hashes, double ratio, TimeSpan seedingTime, TimeSpan inactiveSeedingTime)
        {
            return BuildForm(Url.SetShareLimits(),
                ("hashes", JoinHashes(hashes)),
                ("ratioLimit", ratio.ToString("R", CultureInfo.InvariantCulture)),
                ("seedingTimeLimit", seedingTime.TotalMinutes.ToString("F0")),
                ("inactiveSeedingTimeLimit", inactiveSeedingTime.TotalMinutes.ToString("F0")));
        }

        public override (Uri url, HttpContent request) Start(IEnumerable<string> hashes)
        {
            return BuildForm(Url.Start(),
                ("hashes", JoinHashes(hashes)));
        }

        public override (Uri url, HttpContent request) StartAll()
        {
            return BuildForm(Url.Start(),
                ("hashes", "all"));
        }

        public override (Uri url, HttpContent request) Stop(IEnumerable<string> hashes)
        {
            return BuildForm(Url.Stop(),
                ("hashes", JoinHashes(hashes)));
        }

        public override (Uri url, HttpContent request) StopAll()
        {
            return BuildForm(Url.Stop(),
                ("hashes", "all"));
        }

        public override (Uri url, HttpContent request) BanPeers(IEnumerable<string> peers)
        {
            return BuildForm(Url.BanPeers(),
                ("peers", string.Join("|", peers)));
        }

        public override (Uri url, HttpContent request) AddTorrentPeers(IEnumerable<string> hashes, IEnumerable<string> peers)
        {
            return BuildForm(Url.AddTorrentPeers(),
                ("hashes", JoinHashes(hashes)),
                ("peers", string.Join("|", peers)));
        }

        public override (Uri url, HttpContent request) CreateTags(IEnumerable<string> tags)
        {
            return BuildForm(Url.CreateTags(),
                ("tags", string.Join(",", tags)));
        }

        public override (Uri url, HttpContent request) DeleteTags(IEnumerable<string> tags)
        {
            return BuildForm(Url.DeleteTags(),
                ("tags", string.Join(",", tags)));
        }

        public override (Uri url, HttpContent request) AddTorrentTags(IEnumerable<string> hashes, IEnumerable<string> tags)
        {
            return BuildForm(Url.AddTorrentTags(),
                ("hashes", JoinHashes(hashes)),
                ("tags", string.Join(",", tags)));
        }

        public override (Uri url, HttpContent request) DeleteTorrentTags(IEnumerable<string> hashes, IEnumerable<string> tags)
        {
            return BuildForm(Url.DeleteTorrentTags(),
                ("hashes", JoinHashes(hashes)),
                ("tags", string.Join(",", tags)));
        }

        public override (Uri url, HttpContent request) RenameFile(string hash, int fileId, string newName)
        {
            return BuildForm(Url.RenameFile(),
                ("hash", hash),
                ("id", fileId.ToString()),
                ("name", newName));
        }

        public override (Uri url, HttpContent request) RenameFile(string hash, string oldPath, string newPath)
        {
            return BuildForm(Url.RenameFile(),
                ("hash", hash),
                ("oldPath", oldPath),
                ("newPath", newPath));
        }

        public override (Uri url, HttpContent request) RenameFolder(string hash, string oldPath, string newPath)
        {
            return BuildForm(Url.RenameFolder(),
                ("hash", hash),
                ("oldPath", oldPath),
                ("newPath", newPath));
        }

        public override (Uri url, HttpContent request) AddTorrents(AddTorrentsRequest request)
        {
            var data = AddTorrentsCore(request);

            foreach (var file in request.TorrentFiles)
            {
                data.AddFile("torrents", file, "application/x-bittorrent");
            }

            var urls = string.Join("\n", request.TorrentUrls.Select(url => url.AbsoluteUri));
            data.AddValue("urls", urls);

            return (Url.AddTorrentFiles(), data);
        }

        public override (Uri url, HttpContent request) AddRssFolder(string path)
        {
            return BuildForm(Url.AddRssFolder(),
                ("path", path));
        }

        public override (Uri url, HttpContent request) AddRssFeed(Uri url, string path)
        {
            return BuildForm(Url.AddRssFeed(),
                ("url", url.AbsoluteUri),
                ("path", path));
        }

        public override (Uri url, HttpContent request) DeleteRssItem(string path)
        {
            return BuildForm(Url.DeleteRssItem(),
                ("path", path));
        }

        public override (Uri url, HttpContent request) MoveRssItem(string path, string destinationPath)
        {
            return BuildForm(Url.MoveRssItem(),
                ("itemPath", path),
                ("destPath", destinationPath));
        }

        public override (Uri url, HttpContent request) SetRssAutoDownloadingRule(string name, string ruleDefinition)
        {
            return BuildForm(Url.SetRssAutoDownloadingRule(),
                ("ruleName", name),
                ("ruleDef", ruleDefinition));
        }

        public override (Uri url, HttpContent request) RenameRssAutoDownloadingRule(string name, string newName)
        {
            return BuildForm(Url.RenameRssAutoDownloadingRule(),
                ("ruleName", name),
                ("newRuleName", newName));
        }

        public override (Uri url, HttpContent request) DeleteRssAutoDownloadingRule(string name)
        {
            return BuildForm(Url.DeleteRssAutoDownloadingRule(),
                ("ruleName", name));
        }

        public override (Uri url, HttpContent request) MarkRssItemAsRead(string itemPath, string articleId)
        {
            return articleId == null
                ? BuildForm(Url.MarkRssItemAsRead(), ("itemPath", itemPath))
                : BuildForm(Url.MarkRssItemAsRead(), ("itemPath", itemPath), ("articleId", articleId));
        }

        public override (Uri url, HttpContent request) StartSearch(string pattern, IEnumerable<string> plugins, string category)
        {
            return BuildForm(Url.StartSearch(),
                ("pattern", pattern),
                ("plugins", string.Join("|", plugins)),
                ("category", category));
        }

        public override (Uri url, HttpContent request) StopSearch(int id)
        {
            return BuildForm(Url.StopSearch(),
                ("id", id.ToString()));
        }

        public override (Uri url, HttpContent request) DeleteSearch(int id)
        {
            return BuildForm(Url.DeleteSearch(),
                ("id", id.ToString()));
        }

        public override (Uri url, HttpContent request) InstallSearchPlugins(IEnumerable<Uri> sources)
        {
            return BuildForm(Url.InstallSearchPlugins(),
                ("sources", string.Join("|", sources.Select(u => u.AbsoluteUri))));
        }

        public override (Uri url, HttpContent request) UninstallSearchPlugins(IEnumerable<string> names)
        {
            return BuildForm(Url.UninstallSearchPlugins(),
                ("names", string.Join("|", names)));
        }

        public override (Uri url, HttpContent request) EnableDisableSearchPlugins(IEnumerable<string> names, bool enable)
        {
            return BuildForm(Url.EnableDisableSearchPlugins(),
                ("names", string.Join("|", names)),
                ("enable", enable.ToLowerString()));
        }

        public override (Uri url, HttpContent request) UpdateSearchPlugins()
        {
            return BuildForm(Url.UpdateSearchPlugins());
        }
    }
}
