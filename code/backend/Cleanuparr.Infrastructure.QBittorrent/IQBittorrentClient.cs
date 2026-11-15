using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
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
    public interface IQBittorrentClient
    {
        /// <summary>
        /// Gets or sets the timespan to wait before the request times out.
        /// </summary>
        TimeSpan Timeout { get; set; }

        /// <summary>
        /// Gets the headers which should be sent with each request.
        /// </summary>
        HttpRequestHeaders DefaultRequestHeaders { get; }

        /// <summary>
        /// Authenticates this client with the remote qBittorrent server.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task LoginAsync(
            string username,
            string password,
            CancellationToken token = default);

        /// <summary>
        /// Clears authentication on the remote qBittorrent server.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task LogoutAsync(
            CancellationToken token = default);

        /// <summary>
        /// Gets the torrent list.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task<IReadOnlyList<TorrentInfo>> GetTorrentListAsync(
            TorrentListQuery query = null,
            CancellationToken token = default);

        /// <summary>
        /// Gets the torrent generic properties.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task<TorrentProperties> GetTorrentPropertiesAsync(
            [NotNull] string hash,
            CancellationToken token = default);

        /// <summary>
        /// Gets the torrent contents.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task<IReadOnlyList<TorrentContent>> GetTorrentContentsAsync(
            [NotNull] string hash,
            CancellationToken token = default);

        /// <summary>
        /// Gets the torrent trackers.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task<IReadOnlyList<TorrentTracker>> GetTorrentTrackersAsync(
            [NotNull] string hash,
            CancellationToken token = default);

        /// <summary>
        /// Gets the torrent web seeds.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task<IReadOnlyList<Uri>> GetTorrentWebSeedsAsync(
            [NotNull] string hash,
            CancellationToken token = default);

        /// <summary>
        /// Gets the states of the torrent pieces.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task<IReadOnlyList<TorrentPieceState>> GetTorrentPiecesStatesAsync(
            [NotNull] string hash,
            CancellationToken token = default);

        /// <summary>
        /// Gets the hashes of the torrent pieces.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task<IReadOnlyList<string>> GetTorrentPiecesHashesAsync(
            [NotNull] string hash,
            CancellationToken token = default);

        /// <summary>
        /// Gets the global transfer information.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task<GlobalTransferInfo> GetGlobalTransferInfoAsync(
            CancellationToken token = default);

        /// <summary>
        /// Gets the partial data.
        /// </summary>
        /// <param name="responseId">The response identifier.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task<PartialData> GetPartialDataAsync(
            int responseId = 0,
            CancellationToken token = default);

        /// <summary>
        /// Gets the peer partial data.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="responseId">The response identifier.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task<PeerPartialData> GetPeerPartialDataAsync(
            string hash,
            int responseId = 0,
            CancellationToken token = default);

        /// <summary>
        /// Adds the torrent files to download.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task AddTorrentsAsync(
            [NotNull] AddTorrentFilesRequest request,
            CancellationToken token = default);

        /// <summary>
        /// Adds the torrent URLs or magnet-links to download.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task AddTorrentsAsync(
            [NotNull] AddTorrentUrlsRequest request,
            CancellationToken token = default);

        /// <summary>
        /// Pauses the torrent.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task PauseAsync(
            [NotNull] string hash,
            CancellationToken token = default);

        /// <summary>
        /// Pauses all torrents.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task PauseAllAsync(
            CancellationToken token = default);

        /// <summary>
        /// Resumes the torrent.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task ResumeAsync(
            [NotNull] string hash,
            CancellationToken token = default);

        /// <summary>
        /// Resumes all torrents.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task ResumeAllAsync(
            CancellationToken token = default);

        /// <summary>
        /// Adds the category.
        /// </summary>
        /// <param name="category">The category name.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task AddCategoryAsync(
            [NotNull] string category,
            CancellationToken token = default);

        /// <summary>
        /// Deletes the categories.
        /// </summary>
        /// <param name="categories">The list of categories' names.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task DeleteCategoriesAsync(
            [NotNull, ItemNotNull] IEnumerable<string> categories,
            CancellationToken token = default);

        /// <summary>
        /// Sets the torrent category.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="category">The category.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task SetTorrentCategoryAsync(
            [NotNull, ItemNotNull] IEnumerable<string> hashes,
            [NotNull] string category,
            CancellationToken token = default);

        /// <summary>
        /// Gets the torrent download speed limit.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task<IReadOnlyDictionary<string, long?>> GetTorrentDownloadLimitAsync(
            [NotNull, ItemNotNull] IEnumerable<string> hashes,
            CancellationToken token = default);

        /// <summary>
        /// Sets the torrent download speed limit.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="limit">The limit.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task SetTorrentDownloadLimitAsync(
            [NotNull, ItemNotNull] IEnumerable<string> hashes,
            long limit,
            CancellationToken token = default);

        /// <summary>
        /// Gets the torrent upload speed limit.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task<IReadOnlyDictionary<string, long?>> GetTorrentUploadLimitAsync(
            [NotNull, ItemNotNull] IEnumerable<string> hashes,
            CancellationToken token = default);

        /// <summary>
        /// Sets the torrent upload speed limit.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="limit">The limit.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task SetTorrentUploadLimitAsync(
            [NotNull, ItemNotNull] IEnumerable<string> hashes,
            long limit,
            CancellationToken token = default);

        /// <summary>
        /// Gets the global download speed limit.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task<long?> GetGlobalDownloadLimitAsync(
            CancellationToken token = default);

        /// <summary>
        /// Sets the global download speed limit.
        /// </summary>
        /// <param name="limit">The limit.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task SetGlobalDownloadLimitAsync(
            long limit,
            CancellationToken token = default);

        /// <summary>
        /// Gets the global upload speed limit.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task<long?> GetGlobalUploadLimitAsync(
            CancellationToken token = default);

        /// <summary>
        /// Sets the global upload speed limit.
        /// </summary>
        /// <param name="limit">The limit.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task SetGlobalUploadLimitAsync(
            long limit,
            CancellationToken token = default);

        /// <summary>
        /// Changes the torrent priority.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="change">The priority change.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task ChangeTorrentPriorityAsync(
            [NotNull, ItemNotNull] IEnumerable<string> hashes,
            TorrentPriorityChange change,
            CancellationToken token = default);

        /// <summary>
        /// Sets the file priority.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="fileId">The file identifier.</param>
        /// <param name="priority">The priority.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task SetFilePriorityAsync(
            [NotNull] string hash,
            int fileId,
            TorrentContentPriority priority,
            CancellationToken token = default);

        /// <summary>
        /// Deletes the torrents.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="deleteDownloadedData"><see langword="true"/> to delete the downloaded data.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task DeleteAsync(
            [NotNull, ItemNotNull] IEnumerable<string> hashes,
            bool deleteDownloadedData = false,
            CancellationToken token = default);

        /// <summary>
        /// Sets the location of the torrents.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="newLocation">The new location.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task SetLocationAsync(
            [NotNull, ItemNotNull] IEnumerable<string> hashes,
            [NotNull] string newLocation,
            CancellationToken token = default);

        /// <summary>
        /// Renames the torrent.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="newName">The new name.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task RenameAsync(
            [NotNull] string hash,
            [NotNull] string newName,
            CancellationToken token = default);

        /// <summary>
        /// Adds the trackers to the torrent.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="trackers">The trackers.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task AddTrackersAsync(
            [NotNull] string hash,
            [NotNull, ItemNotNull] IEnumerable<Uri> trackers,
            CancellationToken token = default);

        /// <summary>
        /// Rechecks the torrent.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task RecheckAsync(
            [NotNull] string hash,
            CancellationToken token = default);

        /// <summary>
        /// Gets the server log.
        /// </summary>
        /// <param name="severity">The severity of log entries to return. <see cref="TorrentLogSeverity.All"/> by default.</param>
        /// <param name="afterId">Return the entries with the ID greater than the specified one.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task<IEnumerable<TorrentLogEntry>> GetLogAsync(
            TorrentLogSeverity severity = TorrentLogSeverity.All,
            int afterId = -1,
            CancellationToken token = default);

        /// <summary>
        /// Gets the value indicating whether the alternative speed limits are enabled.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task<bool> GetAlternativeSpeedLimitsEnabledAsync(
            CancellationToken token = default);

        /// <summary>
        /// Toggles the alternative speed limits.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task ToggleAlternativeSpeedLimitsAsync(
            CancellationToken token = default);

        /// <summary>
        /// Sets the automatic torrent management.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="enabled"></param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task SetAutomaticTorrentManagementAsync(
            [NotNull, ItemNotNull] IEnumerable<string> hashes,
            bool enabled,
            CancellationToken token = default);

        /// <summary>
        /// Sets the force start.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="enabled"></param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task SetForceStartAsync(
            [NotNull, ItemNotNull] IEnumerable<string> hashes,
            bool enabled,
            CancellationToken token = default);

        /// <summary>
        /// Sets the super seeding.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="enabled"></param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task SetSuperSeedingAsync(
            [NotNull, ItemNotNull] IEnumerable<string> hashes,
            bool enabled,
            CancellationToken token = default);

        /// <summary>
        /// Toggles the first and last piece priority.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task ToggleFirstLastPiecePrioritizedAsync(
            [NotNull, ItemNotNull] IEnumerable<string> hashes,
            CancellationToken token = default);

        /// <summary>
        /// Toggles the sequential download.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task ToggleSequentialDownloadAsync(
            [NotNull, ItemNotNull] IEnumerable<string> hashes,
            CancellationToken token = default);

        /// <summary>
        /// Gets the current API version of the server.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <remarks>
        /// <para>
        /// For qBittorrent versions before 4.1.0 this method returns version <c>1.x</c>
        /// where <c>x</c> is the value returned by <see cref="GetLegacyApiVersionAsync"/> method.
        /// </para>
        /// <para>
        /// For qBittorrent version starting from 4.1.0 this method returns version <c>x.y</c> or <c>x.y.z</c>
        /// where <c>x >= 2</c>. 
        /// </para>
        /// </remarks>
        /// <returns></returns>
        Task<ApiVersion> GetApiVersionAsync(CancellationToken token = default);
        
        /// <summary>
        /// Gets the current API version of the server for qBittorrent versions up to 4.0.4.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task<int> GetLegacyApiVersionAsync(CancellationToken token = default);

        /// <summary>
        /// Get the minimum API version supported by server. Any application designed to work with an API version greater than or equal to the minimum API version is guaranteed to work.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task<int> GetLegacyMinApiVersionAsync(CancellationToken token = default);

        /// <summary>
        /// Gets the qBittorrent version.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task<Version> GetQBittorrentVersionAsync(CancellationToken token = default);

        /// <summary>
        /// Get the path to the folder where the downloaded files are saved by default.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task<string> GetDefaultSavePathAsync(
            CancellationToken token = default);

        /// <summary>
        /// Gets qBittorrent preferences.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task<Preferences> GetPreferencesAsync(
            CancellationToken token = default);

        /// <summary>
        /// Gets qBittorrent preferences.
        /// </summary>
        /// <param name="preferences">
        /// The prefences to set.
        /// You can set only the properties you want to change and leave the other ones as <see langword="null"/>.
        /// </param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task SetPreferencesAsync(
            Preferences preferences,
            CancellationToken token = default);

        /// <summary>
        /// Quits qBittorrent.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        Task ShutdownApplicationAsync(
            CancellationToken token = default);
    }
}