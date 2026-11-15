using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using QBittorrent.Client.Extensions;

namespace QBittorrent.Client.Internal
{
    internal abstract class BaseRequestProvider : IRequestProvider
    {
        public abstract IUrlProvider Url { get; }
        
        public abstract ApiLevel ApiLevel { get; }

        public virtual (Uri url, HttpContent request) Login(string username, string password)
        {
            return BuildForm(Url.Login(), 
                ("username", username), 
                ("password", password));
        }

        public virtual (Uri url, HttpContent request) Logout() => BuildForm(Url.Logout());

        public virtual (Uri url, HttpContent request) AddTorrents(AddTorrentFilesRequest request)
        {
            var data = AddTorrentsCore(request);
            foreach (var file in request.TorrentFiles)
            {
                data.AddFile("torrents", file, "application/x-bittorrent");
            }

            return (Url.AddTorrentFiles(), data);
        }

        public virtual (Uri url, HttpContent request) AddTorrents(AddTorrentUrlsRequest request)
        {
            var urls = string.Join("\n", request.TorrentUrls.Select(url => url.AbsoluteUri));
            var data = AddTorrentsCore(request)
                .AddValue("urls", urls);

            return (Url.AddTorrentUrls(), data);
        }

        public abstract (Uri url, HttpContent request) AddTorrents(AddTorrentsRequest request);

        protected virtual MultipartFormDataContent AddTorrentsCore(AddTorrentRequestBase request)
        {
            var tags = request.Tags != null ? string.Join(",", request.Tags) : null;
            return new MultipartFormDataContent()
                .AddNonEmptyString("savepath", request.DownloadFolder)
                .AddNonEmptyString("cookie", request.Cookie)
                .AddNonEmptyString("category", request.Category)
                .AddValue("skip_checking", request.SkipHashChecking)
                .AddValue("paused", request.Paused)
                .AddValue("stopped", request.Paused) // "paused" was replaced with "stopped" from API 2.11.2
                .AddNotNullValue("root_folder", request.CreateRootFolder)
                .AddNonEmptyString("rename", request.Rename)
                .AddNotNullValue("upLimit", request.UploadLimit)
                .AddNotNullValue("dlLimit", request.DownloadLimit)
                .AddValue("sequentialDownload", request.SequentialDownload)
                .AddValue("firstLastPiecePrio", request.FirstLastPiecePrioritized)
                .AddValue("autoTMM", request.AutomaticTorrentManagement)
                .AddNotNullValue("ratioLimit", request.RatioLimit)
                .AddNotNullValue("seedingTimeLimit", checked ((int?) request.SeedingTimeLimit?.TotalMinutes))
                .AddNotNullValue("tags", tags)
                .AddNotNullValue("contentLayout", request.ContentLayout);
        }

        public abstract (Uri url, HttpContent request) Pause(IEnumerable<string> hashes);

        public abstract (Uri url, HttpContent request) PauseAll();

        public abstract (Uri url, HttpContent request) Resume(IEnumerable<string> hashes);

        public abstract (Uri url, HttpContent request) ResumeAll();

        public virtual (Uri url, HttpContent request) AddCategory(string category, string savePath = null)
        {
            if (savePath != null)
            {
                return BuildForm(Url.AddCategory(),
                    ("category", category),
                    ("savePath", savePath));
            }
            else
            {
                return BuildForm(Url.AddCategory(),
                    ("category", category));
            }
        }

        public abstract (Uri url, HttpContent request) EditCategory(string category, string savePath);

        public virtual (Uri url, HttpContent request) DeleteCategories(IEnumerable<string> categories)
        {
            if (categories == null)
                throw new ArgumentNullException(nameof(categories));

            var builder = new StringBuilder(4096);
            foreach (var category in categories)
            {
                if (string.IsNullOrWhiteSpace(category))
                    throw new ArgumentException("The collection must not contain nulls or empty strings.", nameof(categories));

                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(category);
            }

            if (builder.Length == 0)
                throw new ArgumentException("The collection must contain at least one category.", nameof(categories));

            var categoriesString = builder.ToString();

            return BuildForm(Url.DeleteCategories(), 
                ("categories", categoriesString));
        }

        public virtual (Uri url, HttpContent request) SetCategory(IEnumerable<string> hashes, string category)
        {
            return BuildForm(Url.SetCategory(),
                ("hashes", JoinHashes(hashes)),
                ("category", category));
        }

        public virtual (Uri url, HttpContent request) GetTorrentDownloadLimit(IEnumerable<string> hashes)
        {
            return BuildForm(Url.GetTorrentDownloadLimit(), 
                ("hashes", JoinHashes(hashes)));
        }

        public virtual (Uri url, HttpContent request) SetTorrentDownloadLimit(IEnumerable<string> hashes, long limit)
        {
            return BuildForm(Url.SetTorrentDownloadLimit(),
                ("hashes", JoinHashes(hashes)),
                ("limit", limit.ToString()));
        }

        public virtual (Uri url, HttpContent request) GetTorrentUploadLimit(IEnumerable<string> hashes)
        {
            return BuildForm(Url.GetTorrentUploadLimit(),
                ("hashes", JoinHashes(hashes)));
        }

        public virtual (Uri url, HttpContent request) SetTorrentUploadLimit(IEnumerable<string> hashes, long limit)
        {
            return BuildForm(Url.SetTorrentUploadLimit(),
                ("hashes", JoinHashes(hashes)),
                ("limit", limit.ToString()));
        }

        public virtual (Uri url, HttpContent request) GetGlobalDownloadLimit()
        {
            return BuildForm(Url.GetGlobalDownloadLimit());
        }

        public virtual (Uri url, HttpContent request) SetGlobalDownloadLimit(long limit)
        {
            return BuildForm(Url.SetGlobalDownloadLimit(),
                ("limit", limit.ToString()));
        }

        public virtual (Uri url, HttpContent request) GetGlobalUploadLimit()
        {
            return BuildForm(Url.GetGlobalUploadLimit());
        }

        public virtual (Uri url, HttpContent request) SetGlobalUploadLimit(long limit)
        {
            return BuildForm(Url.SetGlobalUploadLimit(),
                ("limit", limit.ToString()));
        }

        public virtual (Uri url, HttpContent request) MinTorrentPriority(IEnumerable<string> hashes)
        {
            return BuildForm(Url.MinTorrentPriority(),
                ("hashes", JoinHashes(hashes)));
        }

        public virtual (Uri url, HttpContent request) MaxTorrentPriority(IEnumerable<string> hashes)
        {
            return BuildForm(Url.MaxTorrentPriority(),
                ("hashes", JoinHashes(hashes)));
        }

        public virtual (Uri url, HttpContent request) IncTorrentPriority(IEnumerable<string> hashes)
        {
            return BuildForm(Url.IncTorrentPriority(),
                ("hashes", JoinHashes(hashes)));
        }

        public virtual (Uri url, HttpContent request) DecTorrentPriority(IEnumerable<string> hashes)
        {
            return BuildForm(Url.DecTorrentPriority(),
                ("hashes", JoinHashes(hashes)));
        }

        public virtual (Uri url, HttpContent request) SetFilePriority(string hash, int fileId, TorrentContentPriority priority)
        {
            return BuildForm(Url.SetFilePriority(),
                ("hash", hash),
                ("id", fileId.ToString()),
                ("priority", priority.ToString("D")));
        }

        public abstract (Uri url, HttpContent request) DeleteTorrents(IEnumerable<string> hashes, bool withFiles);

        public virtual (Uri url, HttpContent request) SetLocation(IEnumerable<string> hashes, string newLocation)
        {
            return BuildForm(Url.SetLocation(),
                ("hashes", JoinHashes(hashes)),
                ("location", newLocation));
        }

        public virtual (Uri url, HttpContent request) Rename(string hash, string newName)
        {
            return BuildForm(Url.Rename(),
                ("hash", hash),
                ("name", newName));
        }

        public virtual (Uri url, HttpContent request) AddTrackers(string hash, IEnumerable<Uri> trackers)
        {
            var builder = new StringBuilder(4096);
            foreach (var tracker in trackers)
            {
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (tracker == null)
                    throw new ArgumentException("The collection must not contain nulls.", nameof(trackers));
                if (!tracker.IsAbsoluteUri)
                    throw new ArgumentException("The collection must contain absolute URIs.", nameof(trackers));

                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(tracker.AbsoluteUri);
            }

            if (builder.Length == 0)
                throw new ArgumentException("The collection must contain at least one URI.", nameof(trackers));

            var urls = builder.ToString();

            return BuildForm(Url.AddTrackers(),
                ("hash", hash),
                ("urls", urls));
        }

        public abstract (Uri url, HttpContent request) Recheck(IEnumerable<string> hashes);

        public virtual (Uri url, HttpContent request) ToggleAlternativeSpeedLimits()
        {
            return BuildForm(Url.ToggleAlternativeSpeedLimits());
        }

        public virtual (Uri url, HttpContent request) SetAutomaticTorrentManagement(IEnumerable<string> hashes, bool enabled)
        {
            return BuildForm(Url.SetAutomaticTorrentManagement(),
                ("hashes", JoinHashes(hashes)),
                ("enable", enabled.ToLowerString()));
        }

        public virtual (Uri url, HttpContent request) SetForceStart(IEnumerable<string> hashes, bool enabled)
        {
            return BuildForm(Url.SetForceStart(),
                ("hashes", JoinHashes(hashes)),
                ("value", enabled.ToLowerString()));
        }

        public virtual (Uri url, HttpContent request) SetSuperSeeding(IEnumerable<string> hashes, bool enabled)
        {
            return BuildForm(Url.SetSuperSeeding(),
                ("hashes", JoinHashes(hashes)),
                ("value", enabled.ToLowerString()));
        }

        public virtual (Uri url, HttpContent request) ToggleFirstLastPiecePrioritized(IEnumerable<string> hashes)
        {
            return BuildForm(Url.ToggleFirstLastPiecePrioritized(),
                ("hashes", JoinHashes(hashes)));
        }

        public virtual (Uri url, HttpContent request) ToggleSequentialDownload(IEnumerable<string> hashes)
        {
            return BuildForm(Url.ToggleSequentialDownload(),
                ("hashes", JoinHashes(hashes)));
        }

        public virtual (Uri url, HttpContent request) SetPreferences(string json)
        {
            return BuildForm(Url.SetPreferences(),
                ("json", json));
        }

        public abstract (Uri url, HttpContent request) Reannounce(IEnumerable<string> hashes);

        public abstract (Uri url, HttpContent request) EditTracker(string hash, Uri trackerUrl, Uri newTrackerUrl);

        public abstract (Uri url, HttpContent request) DeleteTrackers(string hash, IEnumerable<Uri> trackerUrls);

        public abstract (Uri url, HttpContent request) SetFilePriority(string hash, IEnumerable<int> fileIds, TorrentContentPriority priority);

        public abstract (Uri url, HttpContent request) SetShareLimits(IEnumerable<string> hashes, double ratio, TimeSpan seedingTime);

        public abstract (Uri url, HttpContent request) SetShareLimits(IEnumerable<string> hashes, double ratio, TimeSpan seedingTime, TimeSpan inactiveSeedingTime);

        public abstract (Uri url, HttpContent request) Start(IEnumerable<string> hashes);

        public abstract (Uri url, HttpContent request) StartAll();

        public abstract (Uri url, HttpContent request) Stop(IEnumerable<string> hashes);

        public abstract (Uri url, HttpContent request) StopAll();

        public abstract (Uri url, HttpContent request) BanPeers(IEnumerable<string> peers);
        
        public abstract (Uri url, HttpContent request) AddTorrentPeers(IEnumerable<string> hashes, IEnumerable<string> peers);
        
        public abstract (Uri url, HttpContent request) CreateTags(IEnumerable<string> tags);
        
        public abstract (Uri url, HttpContent request) DeleteTags(IEnumerable<string> tags);
        
        public abstract (Uri url, HttpContent request) AddTorrentTags(IEnumerable<string> hashes, IEnumerable<string> tags);
        
        public abstract (Uri url, HttpContent request) DeleteTorrentTags(IEnumerable<string> hashes, IEnumerable<string> tags);
       
        public abstract (Uri url, HttpContent request) RenameFile(string hash, int fileId, string newName);

        public abstract (Uri url, HttpContent request) RenameFile(string hash, string oldPath, string newPath);

        public abstract (Uri url, HttpContent request) RenameFolder(string hash, string oldPath, string newPath);

        public abstract (Uri url, HttpContent request) AddRssFolder(string path);

        public abstract (Uri url, HttpContent request) AddRssFeed(Uri url, string path);

        public abstract (Uri url, HttpContent request) DeleteRssItem(string path);

        public abstract (Uri url, HttpContent request) MoveRssItem(string path, string destinationPath);

        public abstract (Uri url, HttpContent request) SetRssAutoDownloadingRule(string name, string ruleDefinition);

        public abstract (Uri url, HttpContent request) RenameRssAutoDownloadingRule(string name, string newName);

        public abstract (Uri url, HttpContent request) DeleteRssAutoDownloadingRule(string name);

        public abstract (Uri url, HttpContent request) MarkRssItemAsRead(string itemPath, string articleId);

        public abstract (Uri url, HttpContent request) StartSearch(string pattern, IEnumerable<string> plugins, string category);

        public abstract (Uri url, HttpContent request) StopSearch(int id);

        public abstract (Uri url, HttpContent request) DeleteSearch(int id);

        public abstract (Uri url, HttpContent request) InstallSearchPlugins(IEnumerable<Uri> sources);

        public abstract (Uri url, HttpContent request) UninstallSearchPlugins(IEnumerable<string> names);

        public abstract (Uri url, HttpContent request) EnableDisableSearchPlugins(IEnumerable<string> names, bool enable);

        public abstract (Uri url, HttpContent request) UpdateSearchPlugins();

        protected (Uri, HttpContent) BuildForm(Uri uri, params (string key, string value)[] fields)
        {
            return (uri, new CompatibleFormUrlEncodedContent(fields));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected string JoinHashes(IEnumerable<string> hashes) => string.Join("|", hashes);
    }
}
