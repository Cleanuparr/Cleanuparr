using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace QBittorrent.Client
{
    /// <summary>
    /// Provides access to qBittorrent remote API.
    /// </summary>
    /// <seealso cref="QBittorrentClient"/>
    /// <seealso cref="QBittorrentClientExtensions"/>
    /// <seealso cref="IQBittorrentClient2"/>
    public interface IQBittorrentClient2 : IQBittorrentClient
    {
        /// <summary>
        /// Gets the peer log.
        /// </summary>
        [ApiLevel(ApiLevel.V2)]
        Task<IEnumerable<PeerLogEntry>> GetPeerLogAsync(
            int afterId = -1,
            CancellationToken token = default);

        /// <summary>
        /// Adds the torrents to download.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2)]
        Task AddTorrentsAsync(
            [NotNull] AddTorrentsRequest request,
            CancellationToken token = default);

        /// <summary>
        /// Deletes all torrents.
        /// </summary>
        /// <param name="deleteDownloadedData"><see langword="true"/> to delete the downloaded data.</param>
        /// <param name="token">The cancellation token.</param>
        [ApiLevel(ApiLevel.V2)]
        Task DeleteAsync(
            bool deleteDownloadedData = false,
            CancellationToken token = default);

        /// <summary>
        /// Rechecks all torrents.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2)]
        Task RecheckAsync(
            CancellationToken token = default);

        /// <summary>
        /// Rechecks the torrents.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task RecheckAsync(
            [NotNull, ItemNotNull] IEnumerable<string> hashes,
            CancellationToken token = default);

        /// <summary>
        /// Reannounces all torrents.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.0.2")]
        Task ReannounceAsync(
            CancellationToken token = default);

        /// <summary>
        /// Reannounces the torrents.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="token">The token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.0.2")]
        Task ReannounceAsync(
            [NotNull, ItemNotNull] IEnumerable<string> hashes,
            CancellationToken token = default);

        /// <summary>
        /// Pauses all torrents.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        /// <remarks>This method supersedes <see cref="IQBittorrentClient.PauseAllAsync"/>.</remarks>
        [ApiLevel(ApiLevel.V1)]
        Task PauseAsync(
            CancellationToken token = default);

        /// <summary>
        /// Pauses the torrents.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2)]
        Task PauseAsync(
            [NotNull, ItemNotNull] IEnumerable<string> hashes,
            CancellationToken token = default);

        /// <summary>
        /// Resumes all torrents.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        /// <remarks>This method supersedes <see cref="IQBittorrentClient.ResumeAllAsync"/>.</remarks>
        [ApiLevel(ApiLevel.V1)]
        Task ResumeAsync(
            CancellationToken token = default);

        /// <summary>
        /// Resumes the torrents.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2)]
        Task ResumeAsync(
            [NotNull, ItemNotNull] IEnumerable<string> hashes,
            CancellationToken token = default);

        /// <summary>
        /// Changes the torrent priority for all torrents.
        /// </summary>
        /// <param name="change">The priority change.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2)]
        Task ChangeTorrentPriorityAsync(
            TorrentPriorityChange change,
            CancellationToken token = default);

        /// <summary>
        /// Sets the torrent download speed limit for all torrents.
        /// </summary>
        /// <param name="limit">The limit.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2)]
        Task SetTorrentDownloadLimitAsync(
            long limit,
            CancellationToken token = default);

        /// <summary>
        /// Sets the torrent upload speed limit for all torrents.
        /// </summary>
        /// <param name="limit">The limit.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2)]
        Task SetTorrentUploadLimitAsync(
            long limit,
            CancellationToken token = default);

        /// <summary>
        /// Sets the location of all torrents.
        /// </summary>
        /// <param name="newLocation">The new location.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2)]
        Task SetLocationAsync(
            [NotNull] string newLocation,
            CancellationToken token = default);

        /// <summary>
        /// Sets the torrent category for all torrents.
        /// </summary>
        /// <param name="category">The category.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2)]
        Task SetTorrentCategoryAsync(
            [NotNull] string category,
            CancellationToken token = default);

        /// <summary>
        /// Sets the automatic torrent management for all torrents.
        /// </summary>
        /// <param name="enabled"></param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2)]
        Task SetAutomaticTorrentManagementAsync(
            bool enabled,
            CancellationToken token = default);

        /// <summary>
        /// Toggles the sequential download for all torrents.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2)]
        Task ToggleSequentialDownloadAsync(
            CancellationToken token = default);

        /// <summary>
        /// Toggles the first and last piece priority for all torrents.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2)]
        Task ToggleFirstLastPiecePrioritizedAsync(
            CancellationToken token = default);

        /// <summary>
        /// Sets the force start for all torrents.
        /// </summary>
        /// <param name="enabled"></param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2)]
        Task SetForceStartAsync(
            bool enabled,
            CancellationToken token = default);

        /// <summary>
        /// Sets the super seeding for all torrents.
        /// </summary>
        /// <param name="enabled"></param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2)]
        Task SetSuperSeedingAsync(
            bool enabled,
            CancellationToken token = default);

        /// <summary>
        /// Adds the category.
        /// </summary>
        /// <param name="category">The category name.</param>
        /// <param name="savePath">The save path for the torrents belonging to this category.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.0")]
        Task AddCategoryAsync(
            [NotNull] string category,
            string savePath,
            CancellationToken token = default);

        /// <summary>
        /// Changes the category save path.
        /// </summary>
        /// <param name="category">The category name.</param>
        /// <param name="savePath">The save path for the torrents belonging to this category.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.0")]
        Task EditCategoryAsync(
            [NotNull] string category,
            string savePath,
            CancellationToken token = default);

        /// <summary>
        /// Gets all categories.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.1")]
        Task<IReadOnlyDictionary<string, Category>> GetCategoriesAsync(
            CancellationToken token = default);

        /// <summary>
        /// Changes tracker URL.
        /// </summary>
        /// <param name="hash">The hash of the torrent.</param>
        /// <param name="trackerUrl">The tracker URL you want to edit.</param>
        /// <param name="newTrackerUrl">The new URL to replace the <paramref name="trackerUrl"/>.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.2.0")]
        Task EditTrackerAsync(
            [NotNull] string hash,
            [NotNull] Uri trackerUrl,
            [NotNull] Uri newTrackerUrl,
            CancellationToken token = default);

        /// <summary>
        /// Removes the trackers from the torrent.
        /// </summary>
        /// <param name="hash">The hash of the torrent.</param>
        /// <param name="trackerUrls">The tracker URLs you want to remove.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.2.0")]
        Task DeleteTrackersAsync(
            [NotNull] string hash,
            [NotNull, ItemNotNull] IEnumerable<Uri> trackerUrls,
            CancellationToken token = default);

        /// <summary>
        /// Sets the file priority for multiple files.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="fileIds">The file identifiers.</param>
        /// <param name="priority">The priority.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.2.0")]
        Task SetFilePriorityAsync(
            [NotNull] string hash,
            [NotNull] IEnumerable<int> fileIds,
            TorrentContentPriority priority,
            CancellationToken token = default);

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
        Task SetShareLimitsAsync(
            [NotNull, ItemNotNull] IEnumerable<string> hashes,
            double ratio,
            TimeSpan seedingTime,
            CancellationToken token = default);

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
        Task SetShareLimitsAsync(
            [NotNull, ItemNotNull] IEnumerable<string> hashes,
            double ratio,
            TimeSpan seedingTime,
            TimeSpan inactiveSeedingTime,
            CancellationToken token = default);

        /// <summary>
        /// Gets the list of network interfaces on the qBittorrent machine.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        Task<IReadOnlyList<NetInterface>> GetNetworkInterfacesAsync(
            CancellationToken token = default);

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
        Task<IReadOnlyList<string>> GetNetworkInterfaceAddressesAsync(
            string networkInterfaceId,
            CancellationToken token = default);

        /// <summary>
        /// Gets the build information.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        Task<BuildInfo> GetBuildInfoAsync(
            CancellationToken token = default);

        /// <summary>
        /// Bans peers.
        /// </summary>
        /// <param name="peers">The list of peers to ban. The peers must be in form <c>ip:port</c>.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        Task BanPeersAsync(
            [NotNull, ItemNotNull] IEnumerable<string> peers,
            CancellationToken token = default);

        /// <summary>
        /// Bans peers.
        /// </summary>
        /// <param name="peers">The list of peers to ban.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        Task BanPeersAsync(
            [NotNull, ItemNotNull] IEnumerable<IPEndPoint> peers,
            CancellationToken token = default);

        /// <summary>
        /// Adds peers to the torrents.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="peers">The list of peers to ban. The peers must be in form <c>ip:port</c>.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        Task<IReadOnlyDictionary<string, PeerAddResult>> AddTorrentPeersAsync(
            [NotNull, ItemNotNull] IEnumerable<string> hashes,
            [NotNull, ItemNotNull] IEnumerable<string> peers,
            CancellationToken token = default);

        /// <summary>
        /// Adds peers to the torrents.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="peers">The list of peers to ban.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        Task<IReadOnlyDictionary<string, PeerAddResult>> AddTorrentPeersAsync(
            [NotNull, ItemNotNull] IEnumerable<string> hashes,
            [NotNull, ItemNotNull] IEnumerable<IPEndPoint> peers,
            CancellationToken token = default);

        /// <summary>
        /// Creates the tags.
        /// </summary>
        /// <param name="tags">The list of the tags to create.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        Task CreateTagsAsync(
            [NotNull, ItemNotNull] IEnumerable<string> tags,
            CancellationToken token = default);

        /// <summary>
        /// Deletes the tags.
        /// </summary>
        /// <param name="tags">The list of the tags to delete.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        Task DeleteTagsAsync(
            [NotNull, ItemNotNull] IEnumerable<string> tags,
            CancellationToken token = default);

        /// <summary>
        /// Gets the list of the tags.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        Task<IReadOnlyList<string>> GetTagsAsync(
            CancellationToken token = default);

        /// <summary>
        /// Gets the torrent contents.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="indexes">The indexes of the files you want to retrieve.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task<IReadOnlyList<TorrentContent>> GetTorrentContentsAsync(
            [NotNull] string hash,
            IEnumerable<string> indexes,
            CancellationToken token = default);

        /// <summary>
        /// Adds the tags to the torrents.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="tags">The tags.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        Task AddTorrentTagsAsync(
            [NotNull, ItemNotNull] IEnumerable<string> hashes,
            [NotNull, ItemNotNull] IEnumerable<string> tags,
            CancellationToken token = default);

        /// <summary>
        /// Adds the tags to all torrents.
        /// </summary>
        /// <param name="tags">The tags.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        Task AddTorrentTagsAsync(
            [NotNull, ItemNotNull] IEnumerable<string> tags,
            CancellationToken token = default);

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
        Task DeleteTorrentTagsAsync(
            [NotNull, ItemNotNull] IEnumerable<string> hashes,
            [NotNull, ItemNotNull] IEnumerable<string> tags,
            CancellationToken token = default);

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
        Task DeleteTorrentTagsAsync(
            [NotNull, ItemNotNull] IEnumerable<string> tags,
            CancellationToken token = default);

        /// <summary>
        /// Removes all tags from the torrents.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        Task ClearTorrentTagsAsync(
            [NotNull, ItemNotNull] IEnumerable<string> hashes,
            CancellationToken token = default);

        /// <summary>
        /// Removes all tags from all torrents.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        Task ClearTorrentTagsAsync(
            CancellationToken token = default);

        /// <summary>
        /// Renames the file in the torrent.
        /// </summary>
        /// <param name="hash">The hash of the torrent.</param>
        /// <param name="fileId">The ID of the file to rename.</param>
        /// <param name="newName">The new name to use for the file.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        /// <remarks>This overload is not supported starting from API v2.8.0. Use <see cref="RenameFileAsync(string,string,string,CancellationToken)"/> instead.</remarks>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.4.0")]
        [Deprecated("2.8.0")]
        Task RenameFileAsync(
            [NotNull] string hash, 
            int fileId, 
            [NotNull] string newName, 
            CancellationToken token = default);

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
        Task RenameFileAsync(
            [NotNull] string hash,
            [NotNull] string oldPath,
            [NotNull] string newPath,
            CancellationToken token = default);

        /// <summary>
        /// Renames the folder of the torrent.
        /// </summary>
        /// <param name="hash">The hash of the torrent.</param>
        /// <param name="oldPath">The old path of the folder.</param>
        /// <param name="newPath">The new path to use for the folder.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.8.0")]
        Task RenameFolderAsync(
            [NotNull] string hash,
            [NotNull] string oldPath,
            [NotNull] string newPath,
            CancellationToken token = default);

        #region RSS

        /// <summary>
        /// Adds the RSS folder.
        /// </summary>
        /// <param name="path">Full path of added folder.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.0")]
        Task AddRssFolderAsync(
            string path,
            CancellationToken token = default);

        /// <summary>
        /// Adds the RSS feed.
        /// </summary>
        /// <param name="url">The URL of the RSS feed.</param>
        /// <param name="path">The full path of added folder.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.0")]
        Task AddRssFeedAsync(
            Uri url,
            string path = "",
            CancellationToken token = default);

        /// <summary>
        /// Removes the RSS folder or feed.
        /// </summary>
        /// <param name="path">The full path of removed folder or feed.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.0")]
        Task DeleteRssItemAsync(
            string path,
            CancellationToken token = default);

        /// <summary>
        /// Moves or renames the RSS folder or feed.
        /// </summary>
        /// <param name="path">The current full path of the folder or feed.</param>
        /// <param name="newPath">The new path.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.0")]
        Task MoveRssItemAsync(
            string path,
            string newPath,
            CancellationToken token = default);

        /// <summary>
        /// Gets all RSS folders and feeds.
        /// </summary>
        /// <param name="withData"><see langword="true" /> if you need current feed articles.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.0")]
        Task<RssFolder> GetRssItemsAsync(
            bool withData = false,
            CancellationToken token = default);

        /// <summary>
        /// Sets the RSS auto-downloading rule.
        /// </summary>
        /// <param name="name">The rule name.</param>
        /// <param name="rule">The rule definition.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.0")]
        Task SetRssAutoDownloadingRuleAsync(
            string name,
            RssAutoDownloadingRule rule,
            CancellationToken token = default);

        /// <summary>
        /// Renames the RSS auto-downloading rule.
        /// </summary>
        /// <param name="name">The rule name.</param>
        /// <param name="newName">The new rule name.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.0")]
        Task RenameRssAutoDownloadingRuleAsync(
            string name,
            string newName,
            CancellationToken token = default);

        /// <summary>
        /// Deletes the RSS auto-downloading rule.
        /// </summary>
        /// <param name="name">The rule name.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.0")]
        Task DeleteRssAutoDownloadingRuleAsync(
            string name,
            CancellationToken token = default);

        /// <summary>
        /// Gets the RSS auto-downloading rules.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.0")]
        Task<IReadOnlyDictionary<string, RssAutoDownloadingRule>> GetRssAutoDownloadingRulesAsync(
            CancellationToken token = default);

        /// <summary>
        /// Marks the RSS article as read, if <paramref name="articleId"/> is not <see langword="null" />.
        /// Otherwise marks the whole RSS feed as read.
        /// </summary>
        /// <param name="itemPath">Full path of the item.</param>
        /// <param name="articleId">ID of the article.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.5.1")]
        Task MarkRssItemAsReadAsync(
            [NotNull] string itemPath, 
            string articleId = null,
            CancellationToken token = default);

        /// <summary>
        /// Returns all articles that match a rule by feed name.
        /// </summary>
        /// <param name="ruleName">Rule name.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.5.1")]
        Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetMatchingRssArticlesAsync(
            [NotNull] string ruleName,
            CancellationToken token = default);

        #endregion RSS

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
        Task<int> StartSearchAsync(
            [NotNull] string pattern,
            [NotNull, ItemNotNull] IEnumerable<string> plugins,
            [NotNull] string category = "all",
            CancellationToken token = default);

        /// <summary>
        /// Stops torrent search job.
        /// </summary>
        /// <param name="id">The ID of the search job.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.1")]
        Task StopSearchAsync(
            int id,
            CancellationToken token = default);

        /// <summary>
        /// Gets the status of all search jobs.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns>The list containing statuses and the number of found torrents for each search job.</returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.1")]
        Task<IReadOnlyList<SearchStatus>> GetSearchStatusAsync(
            CancellationToken token = default);

        /// <summary>
        /// Gets the status of the search jobs with the specified <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The ID of the search job.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>The object containing the status and the number of found torrents.</returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.1")]
        Task<SearchStatus> GetSearchStatusAsync(
            int id,
            CancellationToken token = default);

        /// <summary>
        /// Gets the results of the search job with the specified <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The ID of the search job.</param>
        /// <param name="offset">Result to start at. A negative number means count backwards (e.g. -2 returns the 2 most recent results).</param>
        /// <param name="limit">The maximal number of results to return. 0 or negative means no limit.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.1")]
        Task<SearchResults> GetSearchResultsAsync(
            int id,
            int offset = 0,
            int limit = 0,
            CancellationToken token = default);

        /// <summary>
        /// Deletes the search job with the specified <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The ID of the search job.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.1")]
        Task DeleteSearchAsync(
            int id,
            CancellationToken token = default);

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
        Task<IReadOnlyList<string>> GetSearchCategoriesAsync(
            [NotNull] string plugin,
            CancellationToken token = default);

        /// <summary>
        /// Gets the installed search plugins.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns>The list of the search plugins.</returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.1")]
        Task<IReadOnlyList<SearchPlugin>> GetSearchPluginsAsync(
            CancellationToken token = default);

        /// <summary>
        /// Installs the search plugins.
        /// </summary>
        /// <param name="sources">URLs of the plugins to install.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        /// <remarks>Plugins can be installed from the local file system using <c>file:///</c> URIs as <paramref name="sources"/>.</remarks>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.1")]
        Task InstallSearchPluginsAsync(
            [NotNull, ItemNotNull] IEnumerable<Uri> sources,
            CancellationToken token = default);

        /// <summary>
        /// Uninstalls the search plugins.
        /// </summary>
        /// <param name="names">Names of the plugins to uninstall.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.1")]
        Task UninstallSearchPluginsAsync(
            [NotNull, ItemNotNull] IEnumerable<string> names,
            CancellationToken token = default);

        /// <summary>
        /// Enables the search plugins.
        /// </summary>
        /// <param name="names">Names of the plugins to enable.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.1")]
        Task EnableSearchPluginsAsync(
            [NotNull, ItemNotNull] IEnumerable<string> names,
            CancellationToken token = default);

        /// <summary>
        /// Disables the search plugins.
        /// </summary>
        /// <param name="names">Names of the plugins to disable.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.1")]
        Task DisableSearchPluginsAsync(
            [NotNull, ItemNotNull] IEnumerable<string> names,
            CancellationToken token = default);

        /// <summary>
        /// Updates the search plugins.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.1")]
        Task UpdateSearchPluginsAsync(
            CancellationToken token = default);

        #endregion Search
    }
}
