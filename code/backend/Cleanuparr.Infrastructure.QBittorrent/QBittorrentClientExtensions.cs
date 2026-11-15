using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using static QBittorrent.Client.Internal.Utils;

namespace QBittorrent.Client
{
    /// <summary>
    /// Provides extension methods for <see cref="IQBittorrentClient"/>.
    /// </summary>
    public static class QBittorrentClientExtensions
    {
        /// <summary>
        /// Deletes the category.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient"/> instance.</param>
        /// <param name="category">The category name.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public static Task DeleteCategoryAsync(
            [NotNull] this IQBittorrentClient client,
            [NotNull] string category,
            CancellationToken token = default)
        {
            if (category == null)
                throw new ArgumentNullException(nameof(category));
            if (string.IsNullOrWhiteSpace(category))
                throw new ArgumentException("The category cannot be empty.", nameof(category));

            return client.DeleteCategoriesAsync(new[] { category }, token);
        }

        /// <summary>
        /// Sets the torrent category.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient"/> instance.</param>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="category">The category.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public static Task SetTorrentCategoryAsync(
            [NotNull] this IQBittorrentClient client,
            [NotNull] string hash,
            [NotNull] string category,
            CancellationToken token = default)
        {
            ValidateHash(hash);
            return client.SetTorrentCategoryAsync(new[] { hash }, category, token);
        }

        /// <summary>
        /// Gets the torrent download speed limit.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient"/> instance.</param>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public static Task<long?> GetTorrentDownloadLimitAsync(
            [NotNull] this IQBittorrentClient client,
            [NotNull] string hash,
            CancellationToken token = default)
        {
            ValidateHash(hash);
            return ExecuteAsync();

            async Task<long?> ExecuteAsync()
            {
                var dict = await client.GetTorrentDownloadLimitAsync(new[] { hash }, token).ConfigureAwait(false);
                return dict?.Values?.SingleOrDefault();
            }
        }

        /// <summary>
        /// Sets the torrent download speed limit.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient"/> instance.</param>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="limit">The limit.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public static Task SetTorrentDownloadLimitAsync(
            [NotNull] this IQBittorrentClient client,
            [NotNull] string hash,
            long limit,
            CancellationToken token = default)
        {
            ValidateHash(hash);
            return client.SetTorrentDownloadLimitAsync(new[] { hash }, limit, token);
        }

        /// <summary>
        /// Gets the torrent upload speed limit.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient"/> instance.</param>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public static Task<long?> GetTorrentUploadLimitAsync(
            [NotNull] this IQBittorrentClient client,
            [NotNull] string hash,
            CancellationToken token = default)
        {
            ValidateHash(hash);
            return ExecuteAsync();

            async Task<long?> ExecuteAsync()
            {
                var dict = await client.GetTorrentUploadLimitAsync(new[] { hash }, token).ConfigureAwait(false);
                return dict?.Values?.SingleOrDefault();
            }
        }

        /// <summary>
        /// Sets the torrent upload speed limit.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient"/> instance.</param>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="limit">The limit.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public static Task SetTorrentUploadLimitAsync(
            [NotNull] this IQBittorrentClient client,
            [NotNull] string hash,
            long limit,
            CancellationToken token = default)
        {
            ValidateHash(hash);
            return client.SetTorrentUploadLimitAsync(new[] { hash }, limit, token);
        }

        /// <summary>
        /// Changes the torrent priority.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient"/> instance.</param>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="change">The priority change.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public static Task ChangeTorrentPriorityAsync(
            [NotNull] this IQBittorrentClient client,
            [NotNull] string hash,
            TorrentPriorityChange change,
            CancellationToken token = default)
        {
            ValidateHash(hash);
            return client.ChangeTorrentPriorityAsync(new[] { hash }, change, token);
        }

        /// <summary>
        /// Deletes the torrent.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient"/> instance.</param>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="deleteDownloadedData"><see langword="true"/> to delete the downloaded data.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public static Task DeleteAsync(
            [NotNull] this IQBittorrentClient client,
            [NotNull] string hash,
            bool deleteDownloadedData = false,
            CancellationToken token = default)
        {
            ValidateHash(hash);
            return client.DeleteAsync(new[] { hash }, deleteDownloadedData, token);
        }

        /// <summary>
        /// Sets the location of torrent.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient"/> instance.</param>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="newLocation">The new location.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public static Task SetLocationAsync(
            [NotNull] this IQBittorrentClient client,
            [NotNull] string hash,
            [NotNull] string newLocation,
            CancellationToken token = default)
        {
            ValidateHash(hash);
            return client.SetLocationAsync(new[] { hash }, newLocation, token);
        }

        /// <summary>
        /// Adds the tracker to the torrent.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient"/> instance.</param>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="tracker">The tracker.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public static Task AddTrackerAsync(
            [NotNull] this IQBittorrentClient client,
            [NotNull] string hash,
            [NotNull] Uri tracker,
            CancellationToken token = default)
        {
            if (tracker == null)
                throw new ArgumentNullException(nameof(tracker));
            if (!tracker.IsAbsoluteUri)
                throw new ArgumentException("The URI must be absolute.", nameof(tracker));

            return client.AddTrackersAsync(hash, new[] { tracker }, token);
        }

        /// <summary>
        /// Sets the automatic torrent management.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient"/> instance.</param>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="enabled"></param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public static Task SetAutomaticTorrentManagementAsync(
            [NotNull] this IQBittorrentClient client,
            [NotNull] string hash,
            bool enabled,
            CancellationToken token = default)
        {
            ValidateHash(hash);
            return client.SetAutomaticTorrentManagementAsync(new[] { hash }, enabled, token);
        }

        /// <summary>
        /// Sets the force start.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient"/> instance.</param>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="enabled"></param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public static Task SetForceStartAsync(
            [NotNull] this IQBittorrentClient client,
            [NotNull] string hash,
            bool enabled,
            CancellationToken token = default)
        {
            ValidateHash(hash);
            return client.SetForceStartAsync(new[] { hash }, enabled, token);
        }

        /// <summary>
        /// Sets the super seeding.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient"/> instance.</param>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="enabled"></param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public static Task SetSuperSeedingAsync(
            [NotNull] this IQBittorrentClient client,
            [NotNull] string hash,
            bool enabled,
            CancellationToken token = default)
        {
            ValidateHash(hash);
            return client.SetSuperSeedingAsync(new[] { hash }, enabled, token);
        }

        /// <summary>
        /// Toggles the first and last piece priority.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient"/> instance.</param>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public static Task ToggleFirstLastPiecePrioritizedAsync(
            [NotNull] this IQBittorrentClient client,
            [NotNull] string hash,
            CancellationToken token = default)
        {
            ValidateHash(hash);
            return client.ToggleFirstLastPiecePrioritizedAsync(new[] { hash }, token);
        }

        /// <summary>
        /// Toggles the sequential download.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient"/> instance.</param>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public static Task ToggleSequentialDownloadAsync(
            [NotNull] this IQBittorrentClient client,
            [NotNull] string hash,
            CancellationToken token = default)
        {
            ValidateHash(hash);
            return client.ToggleSequentialDownloadAsync(new[] { hash }, token);
        }

        /// <summary>
        /// Reannounces the torrent.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient2"/> instance.</param>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.0.2")]
        public static Task ReannounceAsync(
            [NotNull] this IQBittorrentClient2 client,
            [NotNull] string hash,
            CancellationToken token = default)
        {
            ValidateHash(hash);
            return client.ReannounceAsync(new[] { hash }, token);
        }

        /// <summary>
        /// Removes the trackers from the torrent.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient2"/> instance.</param>
        /// <param name="hash">The hash of the torrent.</param>
        /// <param name="trackerUrl">The tracker URL you want to remove.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.2.0")]
        public static Task DeleteTrackerAsync(
            [NotNull] this IQBittorrentClient2 client,
            [NotNull] string hash,
            [NotNull] Uri trackerUrl,
            CancellationToken token = default)
        {
            return client.DeleteTrackersAsync(hash, new[] {trackerUrl}, token);
        }

        /// <summary>
        /// Sets the torrent share limits.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient2"/> instance.</param>
        /// <param name="hash">The torrent hash.</param>
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
        [ApiLevel(ApiLevel.V2, MinVersion = "2.0.1")]
        public static Task SetShareLimitsAsync(
            [NotNull] this IQBittorrentClient2 client,
            [NotNull] string hash,
            double ratio,
            TimeSpan seedingTime,
            CancellationToken token = default)
        {
            ValidateHash(hash);
            return client.SetShareLimitsAsync(new[] { hash }, ratio, seedingTime, token);
        }

        /// <summary>
        /// Sets the torrent share limits.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient2"/> instance.</param>
        /// <param name="hash">The torrent hash.</param>
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
        public static Task SetShareLimitsAsync(
            [NotNull] this IQBittorrentClient2 client,
            [NotNull] string hash,
            double ratio,
            TimeSpan seedingTime,
            TimeSpan inactiveSeedingTime,
            CancellationToken token = default)
        {
            ValidateHash(hash);
            return client.SetShareLimitsAsync(new[] { hash }, ratio, seedingTime, inactiveSeedingTime, token);
        }

        /// <summary>
        /// Gets the list of IP addresses for all available network interfaces.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient2"/> instance.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        public static Task<IReadOnlyList<string>> GetNetworkInterfaceAddressesAsync(
            [NotNull] this IQBittorrentClient2 client,
            CancellationToken token = default)
        {
            return client.GetNetworkInterfaceAddressesAsync(null, token);
        }

        /// <summary>
        /// Gets the list of IP addresses for all available network interfaces.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient2"/> instance.</param>
        /// <param name="networkInterface">
        /// The network interface to retrieve the IP addresses for.
        /// If <see langword="null"/>, the result will include IP addresses for all interfaces.
        /// </param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        public static Task<IReadOnlyList<string>> GetNetworkInterfaceAddressesAsync(
            [NotNull] this IQBittorrentClient2 client,
            [CanBeNull] NetInterface networkInterface,
            CancellationToken token = default)
        {
            return client.GetNetworkInterfaceAddressesAsync(networkInterface?.Id, token);
        }

        /// <summary>
        /// Bans a peer.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient2"/> instance.</param>
        /// <param name="peer">The peer to ban in form <c>ip:port</c>.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        public static Task BanPeerAsync(
            [NotNull] this IQBittorrentClient2 client,
            [NotNull] string peer,
            CancellationToken token = default)
        {
            ValidatePeer(peer);
            return client.BanPeersAsync(new[] {peer}, token);
        }

        /// <summary>
        /// Bans a peer.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient2"/> instance.</param>
        /// <param name="peer">The peer to ban.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        public static Task BanPeerAsync(
            [NotNull] this IQBittorrentClient2 client,
            [NotNull] IPEndPoint peer,
            CancellationToken token = default)
        {
            ValidatePeer(peer);
            return client.BanPeersAsync(new[] { peer }, token);
        }

        /// <summary>
        /// Adds peers to the torrent.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient2"/> instance.</param>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="peers">The list of peers to ban. The peers must be in form <c>ip:port</c>.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        public static async Task<PeerAddResult> AddTorrentPeersAsync(
            [NotNull] this IQBittorrentClient2 client,
            [NotNull] string hash,
            [NotNull, ItemNotNull] IEnumerable<string> peers,
            CancellationToken token = default)
        {
            ValidateHash(hash);

            var results = await client.AddTorrentPeersAsync(new[] { hash }, peers, token).ConfigureAwait(false);
            results.TryGetValue(hash, out var result);
            return result;
        }

        /// <summary>
        /// Adds peer to the torrents.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient2"/> instance.</param>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="peer">The peer to ban in form <c>ip:port</c>.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        public static Task<IReadOnlyDictionary<string, PeerAddResult>> AddTorrentPeerAsync(
            [NotNull] this IQBittorrentClient2 client,
            [NotNull, ItemNotNull] IEnumerable<string> hashes,
            [NotNull] string peer,
            CancellationToken token = default)
        {
            ValidatePeer(peer);
            return client.AddTorrentPeersAsync(hashes, new [] {peer}, token);
        }

        /// <summary>
        /// Adds peer to the torrent.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient2"/> instance.</param>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="peer">The peer to ban in form <c>ip:port</c>.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        public static async Task<PeerAddResult> AddTorrentPeerAsync(
            [NotNull] this IQBittorrentClient2 client,
            [NotNull] string hash,
            [NotNull] string peer,
            CancellationToken token = default)
        {
            ValidateHash(hash);
            ValidatePeer(peer);
            
            var results = await client.AddTorrentPeersAsync(new []{ hash }, new[] { peer }, token).ConfigureAwait(false);
            results.TryGetValue(hash, out var result);
            return result;
        }

        /// <summary>
        /// Adds peers to the torrent.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient2"/> instance.</param>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="peers">The list of peers to ban.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        public static async Task<PeerAddResult> AddTorrentPeersAsync(
            [NotNull] this IQBittorrentClient2 client,
            [NotNull] string hash,
            [NotNull, ItemNotNull] IEnumerable<IPEndPoint> peers,
            CancellationToken token = default)
        {
            ValidateHash(hash);

            var results = await client.AddTorrentPeersAsync(new[] { hash }, peers, token).ConfigureAwait(false);
            results.TryGetValue(hash, out var result);
            return result;
        }

        /// <summary>
        /// Adds peer to the torrents.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient2"/> instance.</param>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="peer">The peer to ban.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        public static Task<IReadOnlyDictionary<string, PeerAddResult>> AddTorrentPeerAsync(
            [NotNull] this IQBittorrentClient2 client,
            [NotNull, ItemNotNull] IEnumerable<string> hashes,
            [NotNull] IPEndPoint peer,
            CancellationToken token = default)
        {
            ValidatePeer(peer);
            return client.AddTorrentPeersAsync(hashes, new[] { peer }, token);
        }

        /// <summary>
        /// Adds peer to the torrent.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient2"/> instance.</param>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="peer">The peer to ban.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        public static async Task<PeerAddResult> AddTorrentPeerAsync(
            [NotNull] this IQBittorrentClient2 client,
            [NotNull] string hash,
            [NotNull] IPEndPoint peer,
            CancellationToken token = default)
        {
            ValidateHash(hash);
            ValidatePeer(peer);

            var results = await client.AddTorrentPeersAsync(new[] { hash }, new[] { peer }, token).ConfigureAwait(false);
            results.TryGetValue(hash, out var result);
            return result;
        }

        /// <summary>
        /// Creates the tag.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient2"/> instance.</param>
        /// <param name="tag">The list of the tags to create.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        public static Task CreateTagAsync(
            [NotNull] this IQBittorrentClient2 client, 
            [NotNull] string tag,
            CancellationToken token = default)
        {
            ValidateTag(tag);
            return client.CreateTagsAsync(new[] { tag }, token);
        }

        /// <summary>
        /// Deletes the tag.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient2"/> instance.</param>
        /// <param name="tag">The list of the tags to delete.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        public static Task DeleteTagAsync(
            [NotNull] this IQBittorrentClient2 client, 
            [NotNull] string tag,
            CancellationToken token = default)
        {
            ValidateTag(tag);
            return client.DeleteTagsAsync(new[] { tag }, token);
        }

        /// <summary>
        /// Adds the tags to the torrent.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient2"/> instance.</param>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="tags">The tags.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        public static Task AddTorrentTagsAsync(
            [NotNull] this IQBittorrentClient2 client,
            [NotNull] string hash,
            [NotNull, ItemNotNull] IEnumerable<string> tags,
            CancellationToken token = default)
        {
            ValidateHash(hash);
            return client.AddTorrentTagsAsync(new[] { hash }, tags, token);
        }

        /// <summary>
        /// Adds the tag to the torrents.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient2"/> instance.</param>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="tag">The tag.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        public static Task AddTorrentTagAsync(
            [NotNull] this IQBittorrentClient2 client,
            [NotNull, ItemNotNull] IEnumerable<string> hashes,
            [NotNull] string tag,
            CancellationToken token = default)
        {
            ValidateTag(tag);
            return client.AddTorrentTagsAsync(hashes, new [] { tag }, token);
        }

        /// <summary>
        /// Adds the tag to the torrent.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient2"/> instance.</param>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="tag">The tag.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        public static Task AddTorrentTagAsync(
            [NotNull] this IQBittorrentClient2 client,
            [NotNull] string hash,
            [NotNull] string tag,
            CancellationToken token = default)
        {
            ValidateHash(hash);
            ValidateTag(tag);
            return client.AddTorrentTagsAsync(new[] { hash }, new[] { tag }, token);
        }

        /// <summary>
        /// Adds the tag to all torrents.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient2"/> instance.</param>
        /// <param name="tag">The tag.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        public static Task AddTorrentTagAsync(
            [NotNull] this IQBittorrentClient2 client,
            [NotNull] string tag,
            CancellationToken token = default)
        {
            ValidateTag(tag);
            return client.AddTorrentTagsAsync(new[] { tag }, token);
        }

        /// <summary>
        /// Removes the specified tags from the torrent.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient2"/> instance.</param>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="tags">The tags.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        public static Task DeleteTorrentTagsAsync(
            [NotNull] this IQBittorrentClient2 client,
            [NotNull] string hash,
            [NotNull, ItemNotNull] IEnumerable<string> tags,
            CancellationToken token = default)
        {
            ValidateHash(hash);
            return client.DeleteTorrentTagsAsync(new[] { hash }, tags, token);
        }

        /// <summary>
        /// Removes the specified tag from the torrents.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient2"/> instance.</param>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="tag">The tag.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        public static Task DeleteTorrentTagAsync(
            [NotNull] this IQBittorrentClient2 client,
            [NotNull, ItemNotNull] IEnumerable<string> hashes,
            [NotNull] string tag,
            CancellationToken token = default)
        {
            ValidateTag(tag);
            return client.DeleteTorrentTagsAsync(hashes, new[] { tag }, token);
        }

        /// <summary>
        /// Removes the specified tag from the torrent.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient2"/> instance.</param>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="tag">The tag.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        public static Task DeleteTorrentTagAsync(
            [NotNull] this IQBittorrentClient2 client,
            [NotNull] string hash,
            [NotNull] string tag,
            CancellationToken token = default)
        {
            ValidateHash(hash);
            ValidateTag(tag);
            return client.DeleteTorrentTagsAsync(new[] { hash }, new[] { tag }, token);
        }

        /// <summary>
        /// Removes the specified tag from all torrents.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient2"/> instance.</param>
        /// <param name="tag">The tag.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        public static Task DeleteTorrentTagAsync(
            [NotNull] this IQBittorrentClient2 client,
            [NotNull] string tag,
            CancellationToken token = default)
        {
            ValidateTag(tag);
            return client.DeleteTorrentTagsAsync(new[] { tag }, token);
        }

        /// <summary>
        /// Removes all tags from the torrent.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient2"/> instance.</param>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.3.0")]
        public static Task ClearTorrentTagsAsync(
            [NotNull] this IQBittorrentClient2 client,
            [NotNull] string hash,
            CancellationToken token = default)
        {
            ValidateHash(hash);
            return client.ClearTorrentTagsAsync(new[] { hash }, token);
        }

        /// <summary>
        /// Starts torrent search job.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient2"/> instance.</param>
        /// <param name="pattern">Pattern to search for (e.g. "Ubuntu 18.04").</param>
        /// <param name="plugin">Plugin to use for searching (e.g. "legittorrents").</param>
        /// <param name="category">
        /// Categories to limit your search to (e.g. "legittorrents").
        /// Available categories depend on the specified <paramref name="plugin"/>.
        /// Also supports <c>&quot;all&quot;</c></param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>The ID of the search job.</returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.1")]
        public static Task<int> StartSearchAsync(
            [NotNull] this IQBittorrentClient2 client,
            [NotNull] string pattern,
            [NotNull] string plugin,
            [NotNull] string category = "all",
            CancellationToken token = default)
        {
            if (plugin == null)
                throw new ArgumentNullException(nameof(plugin));

            return client.StartSearchAsync(pattern, new [] {plugin}, category, token);
        }

        /// <summary>
        /// Starts torrent search job using all or enabled plugins.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient2"/> instance.</param>
        /// <param name="pattern">Pattern to search for (e.g. "Ubuntu 18.04").</param>
        /// <param name="disabledPluginsToo">
        /// <see langword="false" /> to search using all enabled plugins;
        /// <see langword="true" /> to search using all (enabled and disabled) plugins.
        /// </param>
        /// <param name="category">
        /// Categories to limit your search to (e.g. "legittorrents").
        /// Also supports <c>&quot;all&quot;</c></param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>The ID of the search job.</returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.1")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task<int> StartSearchAsync(
            [NotNull] this IQBittorrentClient2 client,
            [NotNull] string pattern,
            bool disabledPluginsToo = false,
            [NotNull] string category = "all",
            CancellationToken token = default)
        {
            return client.StartSearchAsync(pattern, 
                disabledPluginsToo ? SearchPlugin.All : SearchPlugin.Enabled, 
                category, 
                token);
        }

        /// <summary>
        /// Installs the search plugins.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient2"/> instance.</param>
        /// <param name="source">URL of the plugins to install.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        /// <remarks>Plugin can be installed from the local file system using <c>file:///</c> URI as <paramref name="source"/>.</remarks>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.1")]
        public static Task InstallSearchPluginAsync(
            [NotNull] this IQBittorrentClient2 client,
            [NotNull] Uri source,
            CancellationToken token = default)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            return client.InstallSearchPluginsAsync(new[] {source}, token);
        }

        /// <summary>
        /// Uninstalls the search plugins.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient2"/> instance.</param>
        /// <param name="name">Name of the plugin to uninstall.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.1")]
        public static Task UninstallSearchPluginAsync(
            [NotNull] this IQBittorrentClient2 client,
            [NotNull] string name,
            CancellationToken token = default)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            return client.UninstallSearchPluginsAsync(new[] {name}, token);
        }

        /// <summary>
        /// Enables the search plugin.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient2"/> instance.</param>
        /// <param name="name">Name of the plugin to enable.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.1")]
        public static Task EnableSearchPluginAsync(
            [NotNull] this IQBittorrentClient2 client,
            [NotNull] string name,
            CancellationToken token = default)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            return client.EnableSearchPluginsAsync(new[] { name }, token);
        }

        /// <summary>
        /// Disables the search plugin.
        /// </summary>
        /// <param name="client">An <see cref="IQBittorrentClient2"/> instance.</param>
        /// <param name="name">Name of the plugin to disable.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.1")]
        public static Task DisableSearchPluginAsync(
            [NotNull] this IQBittorrentClient2 client,
            [NotNull] string name,
            CancellationToken token = default)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            return client.DisableSearchPluginsAsync(new[] { name }, token);
        }
    }
}
