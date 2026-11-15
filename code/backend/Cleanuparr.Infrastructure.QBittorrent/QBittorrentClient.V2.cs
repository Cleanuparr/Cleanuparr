using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QBittorrent.Client.Extensions;
using QBittorrent.Client.Internal;
using static QBittorrent.Client.Internal.Utils;

namespace QBittorrent.Client
{
    public partial class QBittorrentClient : IQBittorrentClient2
    {
        private static readonly IEnumerable<string> All = new[] { "all" };

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static readonly ApiVersion Version_2_0_1 = new ApiVersion(2, 0, 1);

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static readonly ApiVersion Version_2_0_2 = new ApiVersion(2, 0, 2);

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static readonly ApiVersion Version_2_1_0 = new ApiVersion(2, 1);

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static readonly ApiVersion Version_2_1_1 = new ApiVersion(2, 1, 1);

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static readonly ApiVersion Version_2_2_0 = new ApiVersion(2, 2, 0);

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static readonly ApiVersion Version_2_3_0 = new ApiVersion(2, 3, 0);

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static readonly ApiVersion Version_2_4_0 = new ApiVersion(2, 4, 0);

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static readonly ApiVersion Version_2_5_1 = new ApiVersion(2, 5, 1);

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static readonly ApiVersion Version_2_6_0 = new ApiVersion(2, 6, 0);

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static readonly ApiVersion Version_2_6_1 = new ApiVersion(2, 6, 1);

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static readonly ApiVersion Version_2_6_2 = new ApiVersion(2, 6, 2);

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static readonly ApiVersion Version_2_7_0 = new ApiVersion(2, 7, 0);

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static readonly ApiVersion Version_2_8_0 = new ApiVersion(2, 8, 0);

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static readonly ApiVersion Version_2_9_1 = new ApiVersion(2, 9, 1);

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static readonly ApiVersion Version_2_9_2 = new ApiVersion(2, 9, 2);

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static readonly ApiVersion Version_2_11_0 = new ApiVersion(2, 11, 0);

        /// <summary>
        /// Gets the peer log.
        /// </summary>
        [ApiLevel(ApiLevel.V2)]
        public async Task<IEnumerable<PeerLogEntry>> GetPeerLogAsync(
            int afterId = -1,
            CancellationToken token = default)
        {
            var uri = await BuildUriAsync(p => p.GetPeerLog(afterId), token).ConfigureAwait(false);
            var json = await _client.GetStringWithCancellationAsync(uri, token).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<IEnumerable<PeerLogEntry>>(json);
        }

        /// <summary>
        /// Adds the torrents to download.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2)]
        public Task AddTorrentsAsync(
            [JetBrains.Annotations.NotNull] AddTorrentsRequest request,
            CancellationToken token = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            return PostAsync(p => p.AddTorrents(request), token);
        }

        /// <summary>
        /// Deletes all torrents.
        /// </summary>
        /// <param name="deleteDownloadedData"><see langword="true"/> to delete the downloaded data.</param>
        /// <param name="token">The cancellation token.</param>
        [ApiLevel(ApiLevel.V2)]
        public Task DeleteAsync(
            bool deleteDownloadedData = false,
            CancellationToken token = default)
        {
            return PostAsync(p => p.DeleteTorrents(All, deleteDownloadedData), token, ApiLevel.V2);
        }

        /// <summary>
        /// Rechecks all torrents.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2)]
        public Task RecheckAsync(
            CancellationToken token = default)
        {
            return PostAsync(p => p.Recheck(All), token, ApiLevel.V2);
        }

        /// <summary>
        /// Rechecks the torrents.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task RecheckAsync(
            [JetBrains.Annotations.NotNull, ItemNotNull] IEnumerable<string> hashes,
            CancellationToken token = default)
        {
            ValidateHashes(ref hashes);
            return PostAsync(p => p.Recheck(hashes), token, ApiLevel.V2);
        }

        /// <summary>
        /// Reannounces all torrents.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.0.2")]
        public Task ReannounceAsync(
            CancellationToken token = default)
        {
            return PostAsync(p => p.Reannounce(All), token, ApiLevel.V2, Version_2_0_2);
        }

        /// <summary>
        /// Reannounces the torrents.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="token">The token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.0.2")]
        public Task ReannounceAsync(
            [JetBrains.Annotations.NotNull, ItemNotNull] IEnumerable<string> hashes,
            CancellationToken token = default)
        {
            ValidateHashes(ref hashes);
            return PostAsync(p => p.Reannounce(hashes), token, ApiLevel.V2, Version_2_0_2);
        }

        /// <summary>
        /// Pauses all torrents.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        /// <remarks>This method supersedes <see cref="IQBittorrentClient.PauseAllAsync"/>.</remarks>
        public async Task PauseAsync(
            CancellationToken token = default)
        {
            var apiVersion = await GetApiVersionAsync(token).ConfigureAwait(false);
            if (apiVersion < Version_2_11_0)
            {
                await PostAsync(p => p.PauseAll(), token).ConfigureAwait(false);
            }
            else
            {
                await PostAsync(p => p.StopAll(), token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Pauses the torrents.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task PauseAsync(IEnumerable<string> hashes, CancellationToken token = default)
        {
            ValidateHashes(ref hashes);
            return PauseCoreAsync(hashes, token);
        }

        private async Task PauseCoreAsync(IEnumerable<string> hashes, CancellationToken token)
        {
            var apiVersion = await GetApiVersionAsync(token).ConfigureAwait(false);
            if (apiVersion < Version_2_11_0)
            {
                await PostAsync(p => p.Pause(hashes), token).ConfigureAwait(false);
            }
            else
            {
                await PostAsync(p => p.Stop(hashes), token, ApiLevel.V2).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Resumes all torrents.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        /// <remarks>This method supersedes <see cref="IQBittorrentClient.ResumeAllAsync"/>.</remarks>
        public async Task ResumeAsync(
            CancellationToken token = default)
        {
            var apiVersion = await GetApiVersionAsync(token).ConfigureAwait(false);
            if (apiVersion < Version_2_11_0)
            {
                await PostAsync(p => p.ResumeAll(), token).ConfigureAwait(false);
            }
            else
            {
                await PostAsync(p => p.StartAll(), token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Resumes the torrents.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task ResumeAsync(IEnumerable<string> hashes, CancellationToken token = default)
        {
            ValidateHashes(ref hashes);
            return ResumeCoreAsync(hashes, token);
        }

        private async Task ResumeCoreAsync(IEnumerable<string> hashes, CancellationToken token)
        {
            var apiVersion = await GetApiVersionAsync(token).ConfigureAwait(false);
            if (apiVersion < Version_2_11_0)
            {
                await PostAsync(p => p.Resume(hashes), token).ConfigureAwait(false);
            }
            else
            {
                await PostAsync(p => p.Start(hashes), token, ApiLevel.V2).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Changes the torrent priority for all torrents.
        /// </summary>
        /// <param name="change">The priority change.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2)]
        public Task ChangeTorrentPriorityAsync(
            TorrentPriorityChange change,
            CancellationToken token = default)
        {
            switch (change)
            {
                case TorrentPriorityChange.Minimal:
                    return PostAsync(p => p.MinTorrentPriority(All), token, ApiLevel.V2);
                case TorrentPriorityChange.Increase:
                    return PostAsync(p => p.IncTorrentPriority(All), token, ApiLevel.V2);
                case TorrentPriorityChange.Decrease:
                    return PostAsync(p => p.DecTorrentPriority(All), token, ApiLevel.V2);
                case TorrentPriorityChange.Maximal:
                    return PostAsync(p => p.MaxTorrentPriority(All), token, ApiLevel.V2);
                default:
                    throw new ArgumentOutOfRangeException(nameof(change), change, null);
            }
        }

        /// <summary>
        /// Sets the torrent download speed limit for all torrents.
        /// </summary>
        /// <param name="limit">The limit.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2)]
        public Task SetTorrentDownloadLimitAsync(
            long limit,
            CancellationToken token = default)
        {
            if (limit < 0)
                throw new ArgumentOutOfRangeException(nameof(limit));

            return PostAsync(p => p.SetTorrentDownloadLimit(All, limit), token, ApiLevel.V2);
        }

        /// <summary>
        /// Sets the torrent upload speed limit for all torrents.
        /// </summary>
        /// <param name="limit">The limit.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2)]
        public Task SetTorrentUploadLimitAsync(
            long limit,
            CancellationToken token = default)
        {
            if (limit < 0)
                throw new ArgumentOutOfRangeException(nameof(limit));

            return PostAsync(p => p.SetTorrentUploadLimit(All, limit), token, ApiLevel.V2);
        }

        /// <summary>
        /// Sets the location of all torrents.
        /// </summary>
        /// <param name="newLocation">The new location.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2)]
        public Task SetLocationAsync(
            [JetBrains.Annotations.NotNull] string newLocation,
            CancellationToken token = default)
        {
            if (newLocation == null)
                throw new ArgumentNullException(nameof(newLocation));
            if (string.IsNullOrEmpty(newLocation))
                throw new ArgumentException("The location cannot be an empty string.", nameof(newLocation));

            return PostAsync(p => p.SetLocation(All, newLocation), token, ApiLevel.V2);
        }

        /// <summary>
        /// Sets the torrent category for all torrents.
        /// </summary>
        /// <param name="category">The category.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2)]
        public Task SetTorrentCategoryAsync(
            [JetBrains.Annotations.NotNull] string category,
            CancellationToken token = default)
        {
            if (category == null)
                throw new ArgumentNullException(nameof(category));

            return PostAsync(p => p.SetCategory(All, category), token, ApiLevel.V2);
        }

        /// <summary>
        /// Sets the automatic torrent management for all torrents.
        /// </summary>
        /// <param name="enabled"></param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2)]
        public Task SetAutomaticTorrentManagementAsync(
            bool enabled,
            CancellationToken token = default)
        {
            return PostAsync(p => p.SetAutomaticTorrentManagement(All, enabled), token, ApiLevel.V2);
        }

        /// <summary>
        /// Toggles the sequential download for all torrents.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2)]
        public Task ToggleSequentialDownloadAsync(
            CancellationToken token = default)
        {
            return PostAsync(p => p.ToggleSequentialDownload(All), token, ApiLevel.V2);
        }

        /// <summary>
        /// Toggles the first and last piece priority for all torrents.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2)]
        public Task ToggleFirstLastPiecePrioritizedAsync(
            CancellationToken token = default)
        {
            return PostAsync(p => p.ToggleFirstLastPiecePrioritized(All), token, ApiLevel.V2);
        }

        /// <summary>
        /// Sets the force start for all torrents.
        /// </summary>
        /// <param name="enabled"></param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2)]
        public Task SetForceStartAsync(
            bool enabled,
            CancellationToken token = default)
        {
            return PostAsync(p => p.SetForceStart(All, enabled), token, ApiLevel.V2);
        }

        /// <summary>
        /// Sets the super seeding for all torrents.
        /// </summary>
        /// <param name="enabled"></param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2)]
        public Task SetSuperSeedingAsync(
            bool enabled,
            CancellationToken token = default)
        {
            return PostAsync(p => p.SetSuperSeeding(All, enabled), token, ApiLevel.V2);
        }

        /// <summary>
        /// Adds the category.
        /// </summary>
        /// <param name="category">The category name.</param>
        /// <param name="savePath">The save path for the torrents belonging to this category.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.0")]
        public Task AddCategoryAsync(string category, string savePath, CancellationToken token = default)
        {
            return PostAsync(p => p.AddCategory(category, savePath), token, ApiLevel.V2, Version_2_1_0);
        }

        /// <summary>
        /// Changes the category save path.
        /// </summary>
        /// <param name="category">The category name.</param>
        /// <param name="savePath">The save path for the torrents belonging to this category.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.0")]
        public Task EditCategoryAsync(string category, string savePath, CancellationToken token = default)
        {
            return PostAsync(p => p.EditCategory(category, savePath), token, ApiLevel.V2, Version_2_1_0);
        }

        /// <summary>
        /// Gets all categories.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.1")]
        public Task<IReadOnlyDictionary<string, Category>> GetCategoriesAsync(CancellationToken token = default)
        {
            return GetJsonAsync<IReadOnlyDictionary<string, Category>>(p => p.GetCategories(), token,
                ApiLevel.V2, Version_2_1_1);
        }

        /// <summary>
        /// Changes tracker URL.
        /// </summary>
        /// <param name="hash">The hash of the torrent.</param>
        /// <param name="trackerUrl">The tracker URL you want to edit.</param>
        /// <param name="newTrackerUrl">The new URL to replace the <paramref name="trackerUrl"/>.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.2.0")]
        public Task EditTrackerAsync(string hash, Uri trackerUrl, Uri newTrackerUrl, CancellationToken token = default)
        {
            ValidateHash(hash);
            if (trackerUrl == null)
                throw new ArgumentNullException(nameof(trackerUrl));
            if (newTrackerUrl == null)
                throw new ArgumentNullException(nameof(newTrackerUrl));
            if (!trackerUrl.IsAbsoluteUri)
                throw new ArgumentException(nameof(trackerUrl) + " must be an absolute URI.", nameof(trackerUrl));
            if (!newTrackerUrl.IsAbsoluteUri)
                throw new ArgumentException(nameof(newTrackerUrl) + " must be an absolute URI.", nameof(newTrackerUrl));

            return PostAsync(p => p.EditTracker(hash, trackerUrl, newTrackerUrl), token, ApiLevel.V2, Version_2_2_0);
        }

        /// <summary>
        /// Removes the trackers from the torrent.
        /// </summary>
        /// <param name="hash">The hash of the torrent.</param>
        /// <param name="trackerUrls">The tracker URLs you want to remove.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.2.0")]
        public Task DeleteTrackersAsync(string hash, IEnumerable<Uri> trackerUrls, CancellationToken token = default)
        {
            ValidateHash(hash);
            if (trackerUrls == null)
                throw new ArgumentNullException(nameof(trackerUrls));
            var urls = trackerUrls.ToList();
            if (urls.Any(u => u == null))
                throw new ArgumentException("The tracker URL cannot be null.", nameof(trackerUrls));
            if (urls.Any(u => !u.IsAbsoluteUri))
                throw new ArgumentException("The tracker URLs must be absolute.", nameof(trackerUrls));

            return PostAsync(p => p.DeleteTrackers(hash, urls), token, ApiLevel.V2, Version_2_2_0);
        }

        /// <summary>
        /// Sets the file priority for multiple files.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="fileIds">The file identifiers.</param>
        /// <param name="priority">The priority.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.2.0")]
        public Task SetFilePriorityAsync(string hash, IEnumerable<int> fileIds, TorrentContentPriority priority,
            CancellationToken token = default)
        {
            ValidateHash(hash);
            if (fileIds == null)
                throw new ArgumentNullException(nameof(fileIds));
            if (!Enum.GetValues(typeof(TorrentContentPriority)).Cast<TorrentContentPriority>().Contains(priority))
                throw new ArgumentOutOfRangeException(nameof(priority));

            var ids = fileIds.ToList();
            if (ids.Any(id => id < 0))
                throw new ArgumentException("The file IDs must be non-negative numbers.", nameof(fileIds));

            return PostAsync(p => p.SetFilePriority(hash, ids, priority), token, ApiLevel.V2, Version_2_2_0);
        }

        /// <summary>
        /// Sets the torrent share limits.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="ratio">
        /// The ratio limit.
        /// Use <see cref="ShareLimits.Ratio.Global"/> in order to use global limit.
        /// Use <see cref="ShareLimits.Ratio.Unlimited"/> in order to set no limit.
        /// </param>
        /// <param name="seedingTime">
        /// The seeding time limit.
        /// Use <see cref="ShareLimits.SeedingTime.Global"/> in order to use global limit.
        /// Use <see cref="ShareLimits.SeedingTime.Unlimited"/> in order to set no limit.
        /// </param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        /// <seealso cref="ShareLimits.Ratio" />
        /// <seealso cref="ShareLimits.SeedingTime" />
        [ApiLevel(ApiLevel.V2, MinVersion = "2.0.1", MaxVersion = "2.9.1")]
        public Task SetShareLimitsAsync(IEnumerable<string> hashes, double ratio, TimeSpan seedingTime, CancellationToken token = default)
        {
            ValidateHashes(ref hashes);

            return PostAsync(p => p.SetShareLimits(hashes, ratio, seedingTime), token, ApiLevel.V2, Version_2_0_1, Version_2_9_1);
        }

        /// <summary>
        /// Sets the torrent share limits.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="ratio">
        /// The ratio limit.
        /// Use <see cref="ShareLimits.Ratio.Global"/> in order to use global limit.
        /// Use <see cref="ShareLimits.Ratio.Unlimited"/> in order to set no limit.
        /// </param>
        /// <param name="seedingTime">
        /// The seeding time limit.
        /// Use <see cref="ShareLimits.SeedingTime.Global"/> in order to use global limit.
        /// Use <see cref="ShareLimits.SeedingTime.Unlimited"/> in order to set no limit.
        /// </param>
        /// <param name="inactiveSeedingTime">
        /// The inactive seeding time limit.
        /// Use <see cref="ShareLimits.SeedingTime.Global"/> in order to use global limit.
        /// Use <see cref="ShareLimits.SeedingTime.Unlimited"/> in order to set no limit.
        /// </param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        /// <seealso cref="ShareLimits.Ratio" />
        /// <seealso cref="ShareLimits.SeedingTime" />
        [ApiLevel(ApiLevel.V2, MinVersion = "2.9.2")]
        public Task SetShareLimitsAsync(IEnumerable<string> hashes, double ratio, TimeSpan seedingTime, TimeSpan inactiveSeedingTime, CancellationToken token = default)
        {
            ValidateHashes(ref hashes);

            return PostAsync(p => p.SetShareLimits(hashes, ratio, seedingTime, inactiveSeedingTime), token, ApiLevel.V2, Version_2_9_2);
        }

        /// <summary>
        /// Gets the list of network interfaces on the qBittorrent machine.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        public Task<IReadOnlyList<NetInterface>> GetNetworkInterfacesAsync(CancellationToken token = default)
        {
            return GetJsonAsync<IReadOnlyList<NetInterface>>(p => p.GetNetworkInterfaces(), token, 
                ApiLevel.V2, Version_2_3_0);
        }

        /// <summary>
        /// Gets the list of network interface IP addresses.
        /// </summary>
        /// <param name="networkInterfaceId">
        /// The network interface id to retrieve the IP addresses for.
        /// If <see langword="null"/> or empty, the result will include IP addresses for all interfaces.
        /// </param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        public Task<IReadOnlyList<string>> GetNetworkInterfaceAddressesAsync(string networkInterfaceId, CancellationToken token = default)
        {
            return GetJsonAsync<IReadOnlyList<string>>(p => p.GetNetworkInterfaceAddresses(networkInterfaceId ?? string.Empty), token, 
                ApiLevel.V2, Version_2_3_0);
        }

        /// <summary>
        /// Gets the build information.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        public Task<BuildInfo> GetBuildInfoAsync(CancellationToken token = default)
        {
            return GetJsonAsync<BuildInfo>(p => p.GetBuildInfo(), token, ApiLevel.V2, Version_2_3_0);
        }

        /// <summary>
        /// Bans peers.
        /// </summary>
        /// <param name="peers">The list of peers to ban.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        public Task BanPeersAsync(IEnumerable<string> peers, CancellationToken token = default)
        {
            ValidatePeers(ref peers);

            return PostAsync(p => p.BanPeers(peers), token, ApiLevel.V2, Version_2_3_0);
        }

        /// <summary>
        /// Bans peers.
        /// </summary>
        /// <param name="peers">The list of peers to ban.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        public Task BanPeersAsync(IEnumerable<IPEndPoint> peers, CancellationToken token = default)
        {
            ValidatePeers(ref peers);

            return PostAsync(p => p.BanPeers(peers.Select(x => x.ToString())), token, ApiLevel.V2, Version_2_3_0);
        }

        /// <summary>
        /// Adds peers to the torrents.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="peers">The list of peers to ban. The peers must be in form <c>ip:port</c></param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        public Task<IReadOnlyDictionary<string, PeerAddResult>> AddTorrentPeersAsync(
            IEnumerable<string> hashes, IEnumerable<string> peers, CancellationToken token = default)
        {
            ValidateHashes(ref hashes);
            ValidatePeers(ref peers);

            return PostAsync(p => p.AddTorrentPeers(hashes, peers), token, 
                ParseAddTorrentPeersResponse, ApiLevel.V2, Version_2_3_0);
        }

        /// <summary>
        /// Adds peers to the torrents.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="peers">The list of peers to ban.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        public Task<IReadOnlyDictionary<string, PeerAddResult>> AddTorrentPeersAsync(
            IEnumerable<string> hashes, IEnumerable<IPEndPoint> peers, CancellationToken token = default)
        {
            ValidateHashes(ref hashes);
            ValidatePeers(ref peers);

            return PostAsync(p => p.AddTorrentPeers(hashes, peers.Select(x => x.ToString())), token,
                ParseAddTorrentPeersResponse, ApiLevel.V2, Version_2_3_0);
        }

        private static async Task<IReadOnlyDictionary<string, PeerAddResult>> ParseAddTorrentPeersResponse(HttpResponseMessage response)
        {
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var result = JsonConvert.DeserializeObject<IReadOnlyDictionary<string, PeerAddResult>>(json);
            return result;
        }

        /// <summary>
        /// Creates the tags.
        /// </summary>
        /// <param name="tags">The list of the tags to create.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        public Task CreateTagsAsync(IEnumerable<string> tags, CancellationToken token = default)
        {
            int count = ValidateAndCountTags(ref tags);
            if (count == 0)
                return Task.CompletedTask;

            return PostAsync(p => p.CreateTags(tags), token, ApiLevel.V2, Version_2_3_0);
        }

        /// <summary>
        /// Deletes the tags.
        /// </summary>
        /// <param name="tags">The list of the tags to delete.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        public Task DeleteTagsAsync(IEnumerable<string> tags, CancellationToken token = default)
        {
            int count = ValidateAndCountTags(ref tags);
            if (count == 0)
                return Task.CompletedTask;

            return PostAsync(p => p.DeleteTags(tags), token, ApiLevel.V2, Version_2_3_0);
        }

        /// <summary>
        /// Gets the list of the tags.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        public Task<IReadOnlyList<string>> GetTagsAsync(CancellationToken token = default)
        {
            return GetJsonAsync<IReadOnlyList<string>>(p => p.GetTags(), token, ApiLevel.V2, Version_2_3_0);
        }

        /// <summary>
        /// Adds the tags to the torrents.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="tags">The tags.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        public Task AddTorrentTagsAsync(IEnumerable<string> hashes, IEnumerable<string> tags, CancellationToken token = default)
        {
            ValidateHashes(ref hashes);
            int count = ValidateAndCountTags(ref tags);
            if (count == 0)
                return Task.CompletedTask;

            return PostAsync(p => p.AddTorrentTags(hashes, tags), token, ApiLevel.V2, Version_2_3_0);
        }

        /// <summary>
        /// Adds the tags to all torrents.
        /// </summary>
        /// <param name="tags">The tags.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        public Task AddTorrentTagsAsync(IEnumerable<string> tags, CancellationToken token = default)
        {
            int count = ValidateAndCountTags(ref tags);
            if (count == 0)
                return Task.CompletedTask;

            return PostAsync(p => p.AddTorrentTags(All, tags), token, ApiLevel.V2, Version_2_3_0);
        }

        /// <summary>
        /// Removes the specified tags from the torrents.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="tags">The tags.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        /// <remarks>
        /// If the list of tags is empty, this method is no op.
        /// </remarks>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        public Task DeleteTorrentTagsAsync(IEnumerable<string> hashes, IEnumerable<string> tags, CancellationToken token = default)
        {
            ValidateHashes(ref hashes);
            int count = ValidateAndCountTags(ref tags);
            if (count == 0)
                return Task.CompletedTask;

            return PostAsync(p => p.DeleteTorrentTags(hashes, tags), token, ApiLevel.V2, Version_2_3_0);
        }

        /// <summary>
        /// Removes the specified tags from all torrents.
        /// </summary>
        /// <param name="tags">The tags.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        /// <remarks>
        /// If the list of tags is empty, this method is no op.
        /// </remarks>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        public Task DeleteTorrentTagsAsync(IEnumerable<string> tags, CancellationToken token = default)
        {
            int count = ValidateAndCountTags(ref tags);
            if (count == 0)
                return Task.CompletedTask;

            return PostAsync(p => p.DeleteTorrentTags(All, tags), token, ApiLevel.V2, Version_2_3_0);
        }

        /// <summary>
        /// Removes all tags from the torrents.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        public Task ClearTorrentTagsAsync(IEnumerable<string> hashes, CancellationToken token = default)
        {
            ValidateHashes(ref hashes);
            return PostAsync(p => p.DeleteTorrentTags(hashes, Enumerable.Empty<string>()), token, ApiLevel.V2, Version_2_3_0);
        }

        /// <summary>
        /// Removes all tags from all torrents.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        public Task ClearTorrentTagsAsync(CancellationToken token = default)
        {
            return PostAsync(p => p.DeleteTorrentTags(All, Enumerable.Empty<string>()), token, ApiLevel.V2, Version_2_3_0);
        }

        /// <summary>
        /// Renames the file in the torrent.
        /// </summary>
        /// <param name="hash">The hash of the torrent.</param>
        /// <param name="fileId">The ID of the file to rename.</param>
        /// <param name="newName">he new name to use for the file.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        /// <remarks>This overload is not supported starting from API v2.8.0. Use <see cref="RenameFileAsync(string,string,string,CancellationToken)"/> instead.</remarks>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.4.0")]
        [Deprecated("2.8.0")]
        public Task RenameFileAsync(string hash, int fileId, string newName, CancellationToken token = default)
        {
            ValidateHash(hash);
            if (fileId < 0)
                throw new ArgumentOutOfRangeException(nameof(fileId));
            if (newName == null)
                throw new ArgumentNullException(nameof(newName));

            return PostAsync(p => p.RenameFile(hash, fileId, newName), token, ApiLevel.V2, Version_2_4_0, Version_2_7_0);
        }

        /// <summary>
        /// Renames the folder of the torrent.
        /// </summary>
        /// <param name="hash">The hash of the torrent.</param>
        /// <param name="oldPath">The old path of the file.</param>
        /// <param name="newPath">The new path to use for the file.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        /// <remarks>This overload is not supported starting until API v2.8.0. Use <see cref="RenameFileAsync(string,int,string,CancellationToken)"/> for earlier API versions instead.</remarks>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.8.0")]
        public Task RenameFileAsync(string hash, string oldPath, string newPath, CancellationToken token = default)
        {
            ValidateHash(hash);
            if (oldPath == null)
                throw new ArgumentNullException(nameof(oldPath));
            if (newPath == null)
                throw new ArgumentNullException(nameof(newPath));

            return PostAsync(p => p.RenameFile(hash, oldPath, newPath), token, ApiLevel.V2, Version_2_8_0);
        }

        /// <summary>
        /// Renames the folder of the torrent.
        /// </summary>
        /// <param name="hash">The hash of the torrent.</param>
        /// <param name="oldPath">The old path of the folder.</param>
        /// <param name="newPath">The new path to use for the folder.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.8.0")]
        public Task RenameFolderAsync(string hash, string oldPath, string newPath, CancellationToken token = default)
        {
            ValidateHash(hash);
            if (oldPath == null)
                throw new ArgumentNullException(nameof(oldPath));
            if (newPath == null)
                throw new ArgumentNullException(nameof(newPath));

            return PostAsync(p => p.RenameFolder(hash, oldPath, newPath), token, ApiLevel.V2, Version_2_8_0);
        }

        #region RSS

        /// <summary>
        /// Adds the RSS folder.
        /// </summary>
        /// <param name="path">Full path of added folder.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.0")]
        public Task AddRssFolderAsync(string path, CancellationToken token = default)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("The path cannot be an empty string.", nameof(path));

            return PostAsync(p => p.AddRssFolder(path), token, ApiLevel.V2, Version_2_1_0);
        }

        /// <summary>
        /// Adds the RSS feed.
        /// </summary>
        /// <param name="url">The URL of the RSS feed.</param>
        /// <param name="path">The full path of added folder.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.0")]
        public Task AddRssFeedAsync(Uri url, string path = "", CancellationToken token = default)
        {
            if (url == null)
                throw new ArgumentNullException(nameof(url));
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            return PostAsync(p => p.AddRssFeed(url, path), token, ApiLevel.V2, Version_2_1_0);
        }

        /// <summary>
        /// Removes the RSS folder or feed.
        /// </summary>
        /// <param name="path">The full path of removed folder or feed.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.0")]
        public Task DeleteRssItemAsync(string path, CancellationToken token = default)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("The path cannot be an empty string.", nameof(path));

            return PostAsync(p => p.DeleteRssItem(path), token, ApiLevel.V2, Version_2_1_0);
        }

        /// <summary>
        /// Moves or renames the RSS folder or feed.
        /// </summary>
        /// <param name="path">The current full path of the folder or feed.</param>
        /// <param name="newPath">The new path.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.0")]
        public Task MoveRssItemAsync(string path, string newPath, CancellationToken token = default)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("The path cannot be an empty string.", nameof(path));
            if (newPath == null)
                throw new ArgumentNullException(nameof(newPath));

            return PostAsync(p => p.MoveRssItem(path, newPath), token, ApiLevel.V2, Version_2_1_0);
        }

        /// <summary>
        /// Gets all RSS folders and feeds.
        /// </summary>
        /// <param name="withData">
        ///   <see langword="true" /> if you need current feed articles.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.0")]
        public async Task<RssFolder> GetRssItemsAsync(bool withData = false, CancellationToken token = default)
        {
            var provider = await _requestProvider.GetValueAsync(token).ConfigureAwait(false);
            await EnsureApiVersionAsync(provider, token, ApiLevel.V2, Version_2_1_0).ConfigureAwait(false);

            var uri = provider.Url.GetRssItems(withData);
            var json = await _client.GetStringWithCancellationAsync(uri, token).ConfigureAwait(false);
            var rootObject = JObject.Parse(json);
            return RssParser.Parse(rootObject);
        }

        /// <summary>
        /// Sets the RSS auto-downloading rule.
        /// </summary>
        /// <param name="name">The rule name.</param>
        /// <param name="rule">The rule definition.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.0")]
        public Task SetRssAutoDownloadingRuleAsync(string name, RssAutoDownloadingRule rule, CancellationToken token = default)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));

            var ruleDefinition = JsonConvert.SerializeObject(rule);
            return PostAsync(p => p.SetRssAutoDownloadingRule(name, ruleDefinition), token, ApiLevel.V2, Version_2_1_0);
        }

        /// <summary>
        /// Renames the RSS auto-downloading rule.
        /// </summary>
        /// <param name="name">The rule name.</param>
        /// <param name="newName">The new rule name.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.0")]
        public Task RenameRssAutoDownloadingRuleAsync(string name, string newName, CancellationToken token = default)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (newName == null)
                throw new ArgumentNullException(nameof(newName));

            return PostAsync(p => p.RenameRssAutoDownloadingRule(name, newName), token, ApiLevel.V2, Version_2_1_0);
        }

        /// <summary>
        /// Deletes the RSS auto-downloading rule.
        /// </summary>
        /// <param name="name">The rule name.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.0")]
        public Task DeleteRssAutoDownloadingRuleAsync(string name, CancellationToken token = default)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            return PostAsync(p => p.DeleteRssAutoDownloadingRule(name), token, ApiLevel.V2, Version_2_1_0);
        }

        /// <summary>
        /// Gets the RSS auto-downloading rules.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.0")]
        public Task<IReadOnlyDictionary<string, RssAutoDownloadingRule>> GetRssAutoDownloadingRulesAsync(CancellationToken token = default)
        {
            return GetJsonAsync<IReadOnlyDictionary<string, RssAutoDownloadingRule>>(
                p => p.GetRssAutoDownloadingRules(), token,
                ApiLevel.V2, Version_2_1_0);
        }

        /// <summary>
        /// Marks the RSS article as read, if <paramref name="articleId"/> is not <see langword="null" />.
        /// Otherwise marks the whole RSS feed as read.
        /// </summary>
        /// <param name="itemPath">Full path of the item.</param>
        /// <param name="articleId">ID of the article.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.5.1")]
        public Task MarkRssItemAsReadAsync(string itemPath, string articleId = null, CancellationToken token = default)
        {
            if (itemPath == null)
                throw new ArgumentNullException(nameof(itemPath));

            return PostAsync(p => p.MarkRssItemAsRead(itemPath, articleId), token, ApiLevel.V2, Version_2_5_1);
        }

        /// <summary>
        /// Returns all articles that match a rule by feed name.
        /// </summary>
        /// <param name="ruleName">Rule name.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.5.1")]
        public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetMatchingRssArticlesAsync(string ruleName, CancellationToken token = default)
        {
            if (ruleName == null)
                throw new ArgumentNullException(nameof(ruleName));

            return GetJsonAsync<IReadOnlyDictionary<string, IReadOnlyList<string>>>(p => p.GetMatchingArticles(ruleName), token,
                ApiLevel.V2, Version_2_5_1);
        }

        #endregion

        #region Search

        /// <summary>
        /// Starts torrent search job.
        /// </summary>
        /// <param name="pattern">Pattern to search for (e.g. "Ubuntu 18.04").</param>
        /// <param name="plugins">Plugins to use for searching (e.g. "legittorrents").</param>
        /// <param name="category">
        /// Categories to limit your search to (e.g. "legittorrents").
        /// Available categories depend on the specified <paramref name="plugins"/>.
        /// Also supports <c>&quot;all&quot;</c>
        /// </param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>The ID of the search job.</returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.1")]
        public Task<int> StartSearchAsync(string pattern, IEnumerable<string> plugins, string category = "all", CancellationToken token = default)
        {
            if (pattern == null)
                throw new ArgumentNullException(nameof(pattern));
            if (plugins == null)
                throw new ArgumentNullException(nameof(plugins));
            if (category == null)
                throw new ArgumentNullException(nameof(category));

            return PostAsync(
                p => p.StartSearch(pattern, plugins, category), token, 
                async response =>
                {
                    var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return JObject.Parse(json)["id"].ToObject<int>();
                },
                ApiLevel.V2, Version_2_1_1);
        }

        /// <summary>
        /// Stops torrent search job.
        /// </summary>
        /// <param name="id">The ID of the search job.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.1")]
        public Task StopSearchAsync(int id, CancellationToken token = default)
        {
            return PostAsync(p => p.StopSearch(id), token, ApiLevel.V2, Version_2_1_1);
        }

        /// <summary>
        /// Gets the status of all search jobs.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns>The list containing statuses and the number of found torrents for each search job.</returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.1")]
        public Task<IReadOnlyList<SearchStatus>> GetSearchStatusAsync(CancellationToken token = default)
        {
            return GetJsonAsync<IReadOnlyList<SearchStatus>>(p => p.GetSearchStatus(), token,
                ApiLevel.V2, Version_2_1_1);
        }

        /// <summary>
        /// Gets the status of the search jobs with the specified <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The ID of the search job.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>The object containing the status and the number of found torrents.</returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.1")]
        public Task<SearchStatus> GetSearchStatusAsync(int id, CancellationToken token = default)
        {
            return GetJsonAsync<IReadOnlyList<SearchStatus>, SearchStatus>(
                p => p.GetSearchStatus(id), token,
                list => list.SingleOrDefault(),
                ApiLevel.V2, Version_2_1_1);
        }

        /// <summary>
        /// Gets the results of the search job with the specified <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The ID of the search job.</param>
        /// <param name="offset">Result to start at. A negative number means count backwards (e.g. -2 returns the 2 most recent results).</param>
        /// <param name="limit">The maximal number of results to return. 0 or negative means no limit.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.1")]
        public Task<SearchResults> GetSearchResultsAsync(int id, int offset = 0, int limit = 0, CancellationToken token = default)
        {
            return GetJsonAsync<SearchResults>(
                p => p.GetSearchResults(id, offset, limit), token,
                ApiLevel.V2, Version_2_1_1);
        }

        /// <summary>
        /// Deletes the search job with the specified <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The ID of the search job.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.1")]
        public Task DeleteSearchAsync(int id, CancellationToken token = default)
        {
            return PostAsync(p => p.DeleteSearch(id), token, ApiLevel.V2, Version_2_1_1);
        }

        /// <summary>
        /// Gets the search categories.
        /// </summary>
        /// <param name="plugin">
        /// Name of the plugin (e.g. "legittorrents").
        /// Also supports <see cref="SearchPlugin.All"/> and <see cref="SearchPlugin.Enabled"/>.
        /// </param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>The list of the search categories.</returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.1")]
        [Deprecated("2.6")]
        public Task<IReadOnlyList<string>> GetSearchCategoriesAsync(string plugin, CancellationToken token = default)
        {
            if (plugin == null)
                throw new ArgumentNullException(nameof(plugin));

            return GetJsonAsync<IReadOnlyList<string>>(p => p.GetSearchCategories(plugin), token,
                ApiLevel.V2, Version_2_1_1);
        }

        /// <summary>
        /// Gets the installed search plugins.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns>The list of the search plugins.</returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.1")]
        public Task<IReadOnlyList<SearchPlugin>> GetSearchPluginsAsync(CancellationToken token = default)
        {
            return GetJsonAsync<IReadOnlyList<SearchPlugin>>(p => p.GetSearchPlugins(), token,
                ApiLevel.V2, Version_2_1_1);
        }

        /// <summary>
        /// Installs the search plugins.
        /// </summary>
        /// <param name="sources">URLs of the plugins to install.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        /// <remarks>Plugins can be installed from the local file system using <c>file:///</c> URIs as <paramref name="sources"/>.</remarks>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.1")]
        public Task InstallSearchPluginsAsync(IEnumerable<Uri> sources, CancellationToken token = default)
        {
            if (sources == null)
                throw new ArgumentNullException(nameof(sources));

            return PostAsync(p => p.InstallSearchPlugins(sources), token, ApiLevel.V2, Version_2_1_1);
        }

        /// <summary>
        /// Uninstalls the search plugins.
        /// </summary>
        /// <param name="names">Names of the plugins to uninstall.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.1")]
        public Task UninstallSearchPluginsAsync(IEnumerable<string> names, CancellationToken token = default)
        {
            if (names == null)
                throw new ArgumentNullException(nameof(names));

            return PostAsync(p => p.UninstallSearchPlugins(names), token, ApiLevel.V2, Version_2_1_1);
        }

        /// <summary>
        /// Enables the search plugins.
        /// </summary>
        /// <param name="names">Names of the plugins to enable.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.1")]
        public Task EnableSearchPluginsAsync(IEnumerable<string> names, CancellationToken token = default)
        {
            if (names == null)
                throw new ArgumentNullException(nameof(names));

            return PostAsync(p => p.EnableDisableSearchPlugins(names, true), token, ApiLevel.V2, Version_2_1_1);
        }

        /// <summary>
        /// Disables the search plugins.
        /// </summary>
        /// <param name="names">Names of the plugins to disable.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.1")]
        public Task DisableSearchPluginsAsync(IEnumerable<string> names, CancellationToken token = default)
        {
            if (names == null)
                throw new ArgumentNullException(nameof(names));

            return PostAsync(p => p.EnableDisableSearchPlugins(names, false), token, ApiLevel.V2, Version_2_1_1);
        }

        /// <summary>
        /// Updates the search plugins.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.1")]
        public Task UpdateSearchPluginsAsync(CancellationToken token = default)
        {
            return PostAsync(p => p.UpdateSearchPlugins(), token, ApiLevel.V2, Version_2_1_1);
        }

        #endregion
    }
}
