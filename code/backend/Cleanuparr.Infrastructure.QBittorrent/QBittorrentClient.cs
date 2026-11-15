using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QBittorrent.Client.Converters;
using QBittorrent.Client.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using QBittorrent.Client.Internal;
using static QBittorrent.Client.Internal.Utils;

namespace QBittorrent.Client
{
    /// <summary>
    /// Provides access to qBittorrent remote API.
    /// </summary>
    /// <seealso cref="IDisposable" />
    /// <seealso cref="IQBittorrentClient"/>
    /// <seealso cref="QBittorrentClientExtensions"/>
    public partial class QBittorrentClient : IQBittorrentClient, IDisposable
    {
        /// <summary>
        /// The legacy API version returned by qBittorrent 4.2 or later.
        /// </summary>
        public const int NewApiLegacyFallbackVersion = 25;

        private const int NewApiLegacyVersion = 18;

        private readonly Uri _uri;
        private readonly HttpClient _client;

        private readonly Cached<int> _legacyVersion;
        private readonly Cached<IRequestProvider> _requestProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="QBittorrentClient"/> class.
        /// </summary>
        /// <param name="uri">qBittorrent remote server URI.</param>
        /// <param name="httpClient">HttpClient</param>
        public QBittorrentClient([NotNull] HttpClient httpClient, [NotNull] Uri uri)
            : this(httpClient, uri, ApiLevel.Auto)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="QBittorrentClient"/> class.
        /// </summary>
        /// <param name="httpClient">HttpClient</param>
        /// <param name="uri">The qBittorrent remote server URI.</param>
        /// <param name="apiLevel">The qBittorrent API level.</param>
        /// <param name="client">Custom <see cref="HttpClient"/> instance.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="uri"/> or <paramref name="client"/> is <see langword="null"/>.
        /// </exception>
        public QBittorrentClient([NotNull] HttpClient httpClient, [NotNull] Uri uri, ApiLevel apiLevel)
        {
            _uri = (uri ?? throw new ArgumentNullException(nameof(uri))).EnsureTrailingSlash();
            _client = httpClient;
            // qBittorrent 5.2.0+ resets the connection on requests that send "Expect: 100-continue".
            _client.DefaultRequestHeaders.ExpectContinue = false;

            _legacyVersion = new Cached<int>(GetLegacyApiVersionPrivateAsync);
            _requestProvider = new Cached<IRequestProvider>(GetRequestProvider());

            Func<CancellationToken, Task<IRequestProvider>> GetRequestProvider()
            {
                switch (apiLevel)
                {
                    case ApiLevel.Auto:
                        return async token =>
                            await GetLegacyApiVersionAsync(token).ConfigureAwait(false) < NewApiLegacyVersion
                                ? (IRequestProvider) new Api1RequestProvider(uri)
                                : (IRequestProvider) new Api2RequestProvider(uri);
                    case ApiLevel.V1:
                        return token => Task.FromResult<IRequestProvider>(new Api1RequestProvider(uri));
                    case ApiLevel.V2:
                        return token => Task.FromResult<IRequestProvider>(new Api2RequestProvider(uri));
                    default:
                        throw new ArgumentOutOfRangeException(nameof(apiLevel), apiLevel, null);
                }
            }
        }

        #region Properties

        /// <summary>
        /// Gets or sets the timespan to wait before the request times out.
        /// </summary>
        public TimeSpan Timeout
        {
            get => _client.Timeout;
            set => _client.Timeout = value;
        }

        /// <summary>
        /// Gets the headers which should be sent with each request.
        /// </summary>
        public HttpRequestHeaders DefaultRequestHeaders => _client.DefaultRequestHeaders;

        #endregion  

        #region Authentication

        /// <summary>
        /// Authenticates this client with the remote qBittorrent server.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task LoginAsync(
            string username,
            string password,
            CancellationToken token = default)
        {
            return PostRejectingFailsBodyAsync(p => p.Login(username, password), token);
        }

        /// <summary>
        /// Clears authentication on the remote qBittorrent server.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task LogoutAsync(
            CancellationToken token = default)
        {
            return PostAsync(p => p.Logout(), token);
        }

        #endregion

        #region Get

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
        public async Task<ApiVersion> GetApiVersionAsync(CancellationToken token = default)
        {
            var legacyVersion = await GetLegacyApiVersionAsync(token).ConfigureAwait(false);
            if (legacyVersion < NewApiLegacyVersion)
                return new ApiVersion(1, checked((byte)legacyVersion));

            var uri = BuildUri("api/v2/app/webapiVersion");
            var version = await _client.GetStringWithCancellationAsync(uri, token).ConfigureAwait(false);
            return ApiVersion.Parse(version);
        }

        /// <summary>
        /// Gets the current API version of the server for qBittorrent versions up to 4.0.4.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <remarks>
        /// Starting from version 4.2 qBittorrent does not return a legacy version.
        /// But in order to retain compatibility, this library will return <see cref="NewApiLegacyFallbackVersion"/>.
        /// </remarks>
        /// <returns></returns>
        public Task<int> GetLegacyApiVersionAsync(CancellationToken token = default)
        {
            return _legacyVersion.GetValueAsync(token);
        }

        private async Task<int> GetLegacyApiVersionPrivateAsync(CancellationToken token)
        {
            try
            {
                var uri = BuildUri("version/api");
                var versionString = await _client.GetStringWithCancellationAsync(uri, true, token).ConfigureAwait(false);
                var version = !string.IsNullOrEmpty(versionString) ? Convert.ToInt32(versionString) : NewApiLegacyFallbackVersion;
                return version;
            }
            catch (QBittorrentClientRequestException ex) when (ex.StatusCode == HttpStatusCode.InternalServerError)
            {
                // Fallback for qBittorrent 4.2+ on Linux/macOS
                return NewApiLegacyFallbackVersion;
            }
        }

        /// <summary>
        /// Get the minimum API version supported by server. Any application designed to work with an API version greater than or equal to the minimum API version is guaranteed to work.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <remarks>
        /// Starting from version 4.2 qBittorrent does not return a legacy version.
        /// But in order to retain compatibility, this library will return <see cref="NewApiLegacyFallbackVersion"/>.
        /// </remarks>
        /// <returns></returns>
        public async Task<int> GetLegacyMinApiVersionAsync(CancellationToken token = default)
        {
            var uri = BuildUri("version/api_min");
            var versionString = await _client.GetStringWithCancellationAsync(uri, true, token).ConfigureAwait(false);
            var version = !string.IsNullOrEmpty(versionString) ? Convert.ToInt32(versionString) : NewApiLegacyFallbackVersion;
            return version;
        }

        /// <summary>
        /// Gets the qBittorrent version.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public async Task<Version> GetQBittorrentVersionAsync(CancellationToken token = default)
        {
            var uri = await BuildUriAsync(p => p.QBittorrentVersion(), token).ConfigureAwait(false);
            var version = await _client.GetStringWithCancellationAsync(uri, token).ConfigureAwait(false);
            var match = Regex.Match(version, @"\d+\.\d+(?:\.\d+(?:\.\d+)?)?", RegexOptions.None, TimeSpan.FromSeconds(1));
            return match.Success ? new Version(match.Value) : null;
        }

        /// <summary>
        /// Gets the torrent list.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task<IReadOnlyList<TorrentInfo>> GetTorrentListAsync(
            TorrentListQuery query = null,
            CancellationToken token = default)
        {
            query = query ?? new TorrentListQuery();
            var hashes = query.Hashes;
            if (hashes != null)
            {
                ValidateHashes(ref hashes);
            }

            return ExecuteAsync();

            async Task<IReadOnlyList<TorrentInfo>> ExecuteAsync()
            {
                var stoppedFilterNames = false;
                if (query.Filter == TorrentListFilter.Paused || query.Filter == TorrentListFilter.Resumed)
                {
                    var apiVersion = await GetApiVersionAsync(token).ConfigureAwait(false);
                    stoppedFilterNames = apiVersion >= Version_2_11_0;
                }

                var uri = await BuildUriAsync(p => p.GetTorrentList(
                        query.Filter,
                        query.Category,
                        query.SortBy,
                        query.ReverseSort,
                        query.Limit,
                        query.Offset,
                        hashes,
                        query.Tag,
                        stoppedFilterNames), token)
                    .ConfigureAwait(false);

                var json = await _client.GetStringWithCancellationAsync(uri, token).ConfigureAwait(false);
                var result = JsonConvert.DeserializeObject<TorrentInfo[]>(json);
                return result;
            }
        }

        /// <summary>
        /// Gets the torrent generic properties.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task<TorrentProperties> GetTorrentPropertiesAsync(
            string hash,
            CancellationToken token = default)
        {
            ValidateHash(hash);
            return ExecuteAsync();

            async Task<TorrentProperties> ExecuteAsync()
            {
                var uri = await BuildUriAsync(p => p.GetTorrentProperties(hash), token).ConfigureAwait(false);
                var json = await _client.GetStringWithCancellationAsync(uri, true, token).ConfigureAwait(false);
                var result = JsonConvert.DeserializeObject<TorrentProperties>(json);
                return result;
            }
        }

        /// <summary>
        /// Gets the torrent contents.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task<IReadOnlyList<TorrentContent>> GetTorrentContentsAsync(string hash, CancellationToken token = default) => GetTorrentContentsAsync(hash, null, token);

        /// <summary>
        /// Gets the torrent contents.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="indexes">The indexes of the files you want to retrieve.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task<IReadOnlyList<TorrentContent>> GetTorrentContentsAsync(
        string hash,
        IEnumerable<string> indexes,
        CancellationToken token = default)
        {
            ValidateHash(hash);
            return ExecuteAsync();

            async Task<IReadOnlyList<TorrentContent>> ExecuteAsync()
            {
                var uri = await BuildUriAsync(p => p.GetTorrentContents(hash, indexes), token).ConfigureAwait(false);
                var json = await _client.GetStringWithCancellationAsync(uri, true, token).ConfigureAwait(false);
                var result = JsonConvert.DeserializeObject<TorrentContent[]>(json);
                return result;
            }
        }

        /// <summary>
        /// Gets the torrent trackers.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task<IReadOnlyList<TorrentTracker>> GetTorrentTrackersAsync(
            string hash,
            CancellationToken token = default)
        {
            ValidateHash(hash);
            return ExecuteAsync();

            async Task<IReadOnlyList<TorrentTracker>> ExecuteAsync()
            {
                var uri = await BuildUriAsync(p => p.GetTorrentTrackers(hash), token);
                var json = await _client.GetStringWithCancellationAsync(uri, true, token).ConfigureAwait(false);
                var result = JsonConvert.DeserializeObject<TorrentTracker[]>(json);
                return result;
            }
        }

        /// <summary>
        /// Gets the torrent web seeds.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task<IReadOnlyList<Uri>> GetTorrentWebSeedsAsync(
            string hash,
            CancellationToken token = default)
        {
            ValidateHash(hash);
            return ExecuteAsync();

            async Task<IReadOnlyList<Uri>> ExecuteAsync()
            {
                var uri = await BuildUriAsync(p => p.GetTorrentWebSeeds(hash), token).ConfigureAwait(false);
                var json = await _client.GetStringWithCancellationAsync(uri, true, token).ConfigureAwait(false);
                var result = JsonConvert.DeserializeObject<UrlItem[]>(json);
                return result?.Select(x => x.Url).ToArray();
            }
        }

        /// <summary>
        /// Gets the states of the torrent pieces.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task<IReadOnlyList<TorrentPieceState>> GetTorrentPiecesStatesAsync(
            string hash,
            CancellationToken token = default)
        {
            ValidateHash(hash);
            return ExecuteAsync();

            async Task<IReadOnlyList<TorrentPieceState>> ExecuteAsync()
            {
                var uri = await BuildUriAsync(p => p.GetTorrentPiecesStates(hash), token).ConfigureAwait(false);
                var json = await _client.GetStringWithCancellationAsync(uri, true, token).ConfigureAwait(false);
                var result = JsonConvert.DeserializeObject<TorrentPieceState[]>(json);
                return result;
            }
        }

        /// <summary>
        /// Gets the hashes of the torrent pieces.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task<IReadOnlyList<string>> GetTorrentPiecesHashesAsync(
            string hash,
            CancellationToken token = default)
        {
            ValidateHash(hash);
            return ExecuteAsync();

            async Task<IReadOnlyList<string>> ExecuteAsync()
            {
                var uri = await BuildUriAsync(p => p.GetTorrentPiecesHashes(hash), token).ConfigureAwait(false);
                var json = await _client.GetStringWithCancellationAsync(uri, true, token).ConfigureAwait(false);
                var result = JsonConvert.DeserializeObject<string[]>(json);
                return result;
            }
        }

        /// <summary>
        /// Gets the global transfer information.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public async Task<GlobalTransferInfo> GetGlobalTransferInfoAsync(
            CancellationToken token = default)
        {
            var uri = await BuildUriAsync(p => p.GetGlobalTransferInfo(), token).ConfigureAwait(false);
            var json = await _client.GetStringWithCancellationAsync(uri, token).ConfigureAwait(false);
            var result = JsonConvert.DeserializeObject<GlobalTransferInfo>(json);
            return result;
        }

        /// <summary>
        /// Gets the partial data.
        /// </summary>
        /// <param name="responseId">The response identifier.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public async Task<PartialData> GetPartialDataAsync(
            int responseId = 0,
            CancellationToken token = default)
        {
            var uri = await BuildUriAsync(p => p.GetPartialData(responseId), token).ConfigureAwait(false);
            var json = await _client.GetStringWithCancellationAsync(uri, token).ConfigureAwait(false);
            var result = JsonConvert.DeserializeObject<PartialData>(json);
            return result;
        }

        /// <summary>
        /// Gets the peer partial data.
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="responseId">The response identifier.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task<PeerPartialData> GetPeerPartialDataAsync(
            string hash,
            int responseId = 0,
            CancellationToken token = default )
        {
            ValidateHash(hash);
            return ExecuteAsync();

            async Task<PeerPartialData> ExecuteAsync()
            {
                var uri = await BuildUriAsync(p => p.GetPeerPartialData(hash, responseId), token).ConfigureAwait(false);
                var json = await _client.GetStringWithCancellationAsync(uri, true, token).ConfigureAwait(false);
                var result = JsonConvert.DeserializeObject<PeerPartialData>(json);
                return result;
            }
        }

        /// <summary>
        /// Get the path to the folder where the downloaded files are saved by default.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public async Task<string> GetDefaultSavePathAsync(
            CancellationToken token = default)
        {
            var uri = await BuildUriAsync(p => p.GetDefaultSavePath(), token).ConfigureAwait(false);
            return await _client.GetStringWithCancellationAsync(uri, token).ConfigureAwait(false);
        }

        #endregion

        #region Add

        /// <summary>
        /// Adds the torrent files to download.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task AddTorrentsAsync(
            AddTorrentFilesRequest request,
            CancellationToken token = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            return PostRejectingFailsBodyAsync(p => p.AddTorrents(request), token);
        }

        /// <summary>
        /// Adds the torrent URLs or magnet-links to download.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task AddTorrentsAsync(
            AddTorrentUrlsRequest request,
            CancellationToken token = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            return PostRejectingFailsBodyAsync(p => p.AddTorrents(request), token);
        }

        #endregion

        #region Pause/Resume

        /// <summary>
        /// Pauses the torrent.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task PauseAsync(
            string hash,
            CancellationToken token = default)
        {
            ValidateHash(hash);
            return PauseCoreAsync(new[] { hash }, token);
        }

        /// <summary>
        /// Pauses all torrents.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <remarks>This method is obsolete. Use <see cref="PauseAsync(CancellationToken)"/> method instead.</remarks>
        /// <returns></returns>
        [Obsolete("Use PauseAsync(CancellationToken) method instead.")]
        public Task PauseAllAsync(
            CancellationToken token = default)
        {
            return PauseAsync(token);
        }

        /// <summary>
        /// Resumes the torrent.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task ResumeAsync(
            string hash,
            CancellationToken token = default)
        {
            ValidateHash(hash);
            return ResumeCoreAsync(new[] { hash }, token);
        }

        /// <summary>
        /// Resumes all torrents.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <remarks>This method is obsolete. Use <see cref="ResumeAsync(CancellationToken)"/> method instead.</remarks>
        /// <returns></returns>
        [Obsolete("Use ResumeAsync(CancellationToken) method instead.")]
        public Task ResumeAllAsync(
            CancellationToken token = default)
        {
            return ResumeAsync(token);
        }

        #endregion

        #region Categories

        /// <summary>
        /// Adds the category.
        /// </summary>
        /// <param name="category">The category name.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task AddCategoryAsync(
            string category,
            CancellationToken token = default)
        {
            if (category == null)
                throw new ArgumentNullException(nameof(category));
            if (string.IsNullOrWhiteSpace(category))
                throw new ArgumentException("The category cannot be empty.", nameof(category));

            return PostAsync(p => p.AddCategory(category), token);
        }

        /// <summary>
        /// Deletes the categories.
        /// </summary>
        /// <param name="categories">The list of categories' names.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task DeleteCategoriesAsync(
            IEnumerable<string> categories,
            CancellationToken token = default)
        {
            if (categories == null)
                throw new ArgumentNullException(nameof(categories));

            return PostAsync(p => p.DeleteCategories(categories), token);
        }

        /// <summary>
        /// Sets the torrent category.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="category">The category.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task SetTorrentCategoryAsync(
            IEnumerable<string> hashes,
            string category,
            CancellationToken token = default)
        {
            ValidateHashes(ref hashes);
            if (category == null)
                throw new ArgumentNullException(nameof(category));

            return PostAsync(p => p.SetCategory(hashes, category), token);
        }

        #endregion

        #region Limits

        /// <summary>
        /// Gets the torrent download speed limit.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task<IReadOnlyDictionary<string, long?>> GetTorrentDownloadLimitAsync(
            IEnumerable<string> hashes,
            CancellationToken token = default)
        {
            ValidateHashes(ref hashes);
            return PostAsync(p => p.GetTorrentDownloadLimit(hashes), token, response => GetTorrentLimits(response, token));
        }

        /// <summary>
        /// Sets the torrent download speed limit.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="limit">The limit.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task SetTorrentDownloadLimitAsync(
            IEnumerable<string> hashes,
            long limit,
            CancellationToken token = default)
        {
            ValidateHashes(ref hashes);
            if (limit < 0)
                throw new ArgumentOutOfRangeException(nameof(limit));

            return PostAsync(p => p.SetTorrentDownloadLimit(hashes, limit), token);
        }

        /// <summary>
        /// Gets the torrent upload speed limit.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task<IReadOnlyDictionary<string, long?>> GetTorrentUploadLimitAsync(
            IEnumerable<string> hashes,
            CancellationToken token = default)
        {
            ValidateHashes(ref hashes);
            return PostAsync(p => p.GetTorrentUploadLimit(hashes), token, response => GetTorrentLimits(response, token));
        }

        /// <summary>
        /// Sets the torrent upload speed limit.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="limit">The limit.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task SetTorrentUploadLimitAsync(
            IEnumerable<string> hashes,
            long limit,
            CancellationToken token = default)
        {
            ValidateHashes(ref hashes);
            if (limit < 0)
                throw new ArgumentOutOfRangeException(nameof(limit));

            return PostAsync(p => p.SetTorrentUploadLimit(hashes, limit), token);
        }

        /// <summary>
        /// Gets the global download speed limit.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task<long?> GetGlobalDownloadLimitAsync(
            CancellationToken token = default)
        {
            return PostAsync(p => p.GetGlobalDownloadLimit(), token,
                async response =>
                {
                    var strValue = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                    return long.TryParse(strValue, out long value) ? value : (long?)0;
                });
        }

        /// <summary>
        /// Sets the global download speed limit.
        /// </summary>
        /// <param name="limit">The limit.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task SetGlobalDownloadLimitAsync(
            long limit,
            CancellationToken token = default)
        {
            if (limit < 0)
                throw new ArgumentOutOfRangeException(nameof(limit));

            return PostAsync(p => p.SetGlobalDownloadLimit(limit), token);
        }

        /// <summary>
        /// Gets the global upload speed limit.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task<long?> GetGlobalUploadLimitAsync(
            CancellationToken token = default)
        {
            return PostAsync(p => p.GetGlobalUploadLimit(), token,
                async response =>
                {
                    var strValue = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                    return long.TryParse(strValue, out long value) ? value : (long?)0;
                });
        }

        /// <summary>
        /// Sets the global upload speed limit.
        /// </summary>
        /// <param name="limit">The limit.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task SetGlobalUploadLimitAsync(
            long limit,
            CancellationToken token = default)
        {
            if (limit < 0)
                throw new ArgumentOutOfRangeException(nameof(limit));

            return PostAsync(p => p.SetGlobalUploadLimit(limit), token);
        }

        #endregion

        #region Priority

        /// <summary>
        /// Changes the torrent priority.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="change">The priority change.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task ChangeTorrentPriorityAsync(
            IEnumerable<string> hashes,
            TorrentPriorityChange change,
            CancellationToken token = default)
        {
            ValidateHashes(ref hashes);

            switch (change)
            {
                case TorrentPriorityChange.Minimal:
                    return PostAsync(p => p.MinTorrentPriority(hashes), token);
                case TorrentPriorityChange.Increase:
                    return PostAsync(p => p.IncTorrentPriority(hashes), token);
                case TorrentPriorityChange.Decrease:
                    return PostAsync(p => p.DecTorrentPriority(hashes), token);
                case TorrentPriorityChange.Maximal:
                    return PostAsync(p => p.MaxTorrentPriority(hashes), token);
                default:
                    throw new ArgumentOutOfRangeException(nameof(change), change, null);
            }
        }

        /// <summary>
        /// Sets the file priority.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="fileId">The file identifier.</param>
        /// <param name="priority">The priority.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task SetFilePriorityAsync(
            string hash,
            int fileId,
            TorrentContentPriority priority,
            CancellationToken token = default)
        {
            ValidateHash(hash);
            if (fileId < 0)
                throw new ArgumentOutOfRangeException(nameof(fileId));
            if (!Enum.GetValues(typeof(TorrentContentPriority)).Cast<TorrentContentPriority>().Contains(priority))
                throw new ArgumentOutOfRangeException(nameof(priority));

            return PostAsync(p => p.SetFilePriority(hash, fileId, priority), token);
        }

        #endregion

        #region Other

        /// <summary>
        /// Deletes the torrents.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="deleteDownloadedData"><see langword="true"/> to delete the downloaded data.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task DeleteAsync(
            IEnumerable<string> hashes,
            bool deleteDownloadedData = false,
            CancellationToken token = default)
        {
            ValidateHashes(ref hashes);
            return PostAsync(p => p.DeleteTorrents(hashes, deleteDownloadedData), token);
        }

        /// <summary>
        /// Sets the location of the torrents.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="newLocation">The new location.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task SetLocationAsync(
            IEnumerable<string> hashes,
            string newLocation,
            CancellationToken token = default)
        {
            ValidateHashes(ref hashes);
            if (newLocation == null)
                throw new ArgumentNullException(nameof(newLocation));
            if (string.IsNullOrEmpty(newLocation))
                throw new ArgumentException("The location cannot be an empty string.", nameof(newLocation));

            return PostAsync(p => p.SetLocation(hashes, newLocation), token);
        }

        /// <summary>
        /// Renames the torrent.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="newName">The new name.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task RenameAsync(
            string hash,
            string newName,
            CancellationToken token = default)
        {
            ValidateHash(hash);
            if (newName == null)
                throw new ArgumentNullException(nameof(newName));
            if (string.IsNullOrWhiteSpace(newName))
                throw new ArgumentException("The name cannot be an empty string.", nameof(newName));

            return PostAsync(p => p.Rename(hash, newName), token);
        }

        /// <summary>
        /// Adds the trackers to the torrent.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="trackers">The trackers.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task AddTrackersAsync(
            string hash,
            IEnumerable<Uri> trackers,
            CancellationToken token = default)
        {
            ValidateHash(hash);
            ValidateUrls(ref trackers);

            return PostAsync(p => p.AddTrackers(hash, trackers), token);

            void ValidateUrls(ref IEnumerable<Uri> urls)
            {
                if (trackers == null)
                    throw new ArgumentNullException(nameof(trackers));

                var list = new List<Uri>();
                foreach (var url in urls)
                {
                    // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                    if (url == null)
                        throw new ArgumentException("The collection must not contain nulls.", nameof(trackers));
                    if (!url.IsAbsoluteUri)
                        throw new ArgumentException("The collection must contain absolute URIs.", nameof(trackers));

                    list.Add(url);
                }

                if (list.Count == 0)
                    throw new ArgumentException("The collection must contain at least one URI.", nameof(trackers));

                urls = list;
            }
        }

        /// <summary>
        /// Rechecks the torrent.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task RecheckAsync(
            string hash,
            CancellationToken token = default)
        {
            ValidateHash(hash);
            return PostAsync(p => p.Recheck(new[] {hash}), token);
        }

        /// <summary>
        /// Gets the server log.
        /// </summary>
        /// <param name="severity">The severity of log entries to return. <see cref="TorrentLogSeverity.All"/> by default.</param>
        /// <param name="afterId">Return the entries with the ID greater than the specified one.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public async Task<IEnumerable<TorrentLogEntry>> GetLogAsync(
            TorrentLogSeverity severity = TorrentLogSeverity.All,
            int afterId = -1,
            CancellationToken token = default)
        {
            var uri = await BuildUriAsync(p => p.GetLog(severity, afterId), token).ConfigureAwait(false);
            var json = await _client.GetStringWithCancellationAsync(uri, token).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<IEnumerable<TorrentLogEntry>>(json);
        }

        /// <summary>
        /// Gets the value indicating whether the alternative speed limits are enabled.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public async Task<bool> GetAlternativeSpeedLimitsEnabledAsync(
            CancellationToken token = default)
        {
            var uri = await BuildUriAsync(p => p.GetAlternativeSpeedLimitsEnabled(), token).ConfigureAwait(false);
            var result = await _client.GetStringWithCancellationAsync(uri, token).ConfigureAwait(false);
            return result == "1";
        }

        /// <summary>
        /// Toggles the alternative speed limits.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task ToggleAlternativeSpeedLimitsAsync(
            CancellationToken token = default)
        {
            return PostAsync(p => p.ToggleAlternativeSpeedLimits(), token);
        }

        /// <summary>
        /// Sets the automatic torrent management.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="enabled"></param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task SetAutomaticTorrentManagementAsync(
            IEnumerable<string> hashes,
            bool enabled,
            CancellationToken token = default)
        {
            ValidateHashes(ref hashes);
            return PostAsync(p => p.SetAutomaticTorrentManagement(hashes, enabled), token);
        }

        /// <summary>
        /// Sets the force start.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="enabled"></param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task SetForceStartAsync(
            IEnumerable<string> hashes,
            bool enabled,
            CancellationToken token = default)
        {
            ValidateHashes(ref hashes);
            return PostAsync(p => p.SetForceStart(hashes, enabled), token);
        }

        /// <summary>
        /// Sets the super seeding asynchronous.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="enabled"></param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task SetSuperSeedingAsync(
            IEnumerable<string> hashes,
            bool enabled,
            CancellationToken token = default)
        {
            ValidateHashes(ref hashes);
            return PostAsync(p => p.SetSuperSeeding(hashes, enabled), token);
        }


        /// <summary>
        /// Toggles the first and last piece priority.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task ToggleFirstLastPiecePrioritizedAsync(
            IEnumerable<string> hashes,
            CancellationToken token = default)
        {
            ValidateHashes(ref hashes);
            return PostAsync(p => p.ToggleFirstLastPiecePrioritized(hashes), token);
        }

        /// <summary>
        /// Toggles the sequential download.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task ToggleSequentialDownloadAsync(
            IEnumerable<string> hashes,
            CancellationToken token = default)
        {
            ValidateHashes(ref hashes);
            return PostAsync(p => p.ToggleSequentialDownload(hashes), token);
        }

        /// <summary>
        /// Gets qBittorrent preferences.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public async Task<Preferences> GetPreferencesAsync(
            CancellationToken token = default)
        {
            var uri = await BuildUriAsync(p => p.GetPreferences(), token).ConfigureAwait(false);
            var response = await _client.GetStringWithCancellationAsync(uri, token).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<Preferences>(response);
        }

        /// <summary>
        /// Gets qBittorrent preferences.
        /// </summary>
        /// <param name="preferences">
        /// The prefences to set.
        /// You can set only the properties you want to change and leave the other ones as <see langword="null"/>.
        /// </param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public async Task SetPreferencesAsync(
            Preferences preferences,
            CancellationToken token = default)
        {
            if (preferences == null)
                throw new ArgumentNullException(nameof(preferences));

            var apiVersion = await GetApiVersionAsync(token).ConfigureAwait(false);
            var settings = new JsonSerializerSettings
            {
                Converters = { new ProxyTypeConverter(apiVersion) }
            };

            string json;
            if (apiVersion >= new ApiVersion(2, 9)
                && (preferences.ProxyType == ProxyType.HttpAuth || preferences.ProxyType == ProxyType.Socks5Auth))
            {
                var tree = JObject.FromObject(preferences, JsonSerializer.Create(settings));
                tree["proxy_auth_enabled"] = true;
                json = tree.ToString(Formatting.None);
            }
            else
            {
                json = JsonConvert.SerializeObject(preferences, settings);
            }

            await PostAsync(p => p.SetPreferences(json), token);
        }


        /// <summary>
        /// Quits qBittorrent.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public async Task ShutdownApplicationAsync(CancellationToken token = default)
        {
            await PostAsync(p => (p.Url.ShutdownApplication(), new CompatibleFormUrlEncodedContent(Enumerable.Empty<(string key, string value)>())), token).ConfigureAwait(false);
        }

        #endregion

        /// <inheritdoc />
        public void Dispose()
        {
            _client?.Dispose();
        }

        private Uri BuildUri(string path, params (string key, string value)[] parameters)
        {
            return new Uri(_uri, path).WithQueryParameters(parameters);
        }

        private async Task<Uri> BuildUriAsync(Func<IUrlProvider, Uri> builder, CancellationToken token = default)
        {
            var provider = await _requestProvider.GetValueAsync(token).ConfigureAwait(false);
            return builder(provider.Url);
        }

        private async Task PostAsync(Func<IRequestProvider, (Uri, HttpContent)> builder,
            CancellationToken token,
            ApiLevel minApiLevel = ApiLevel.V1,
            ApiVersion minApiVersion = default,
            ApiVersion? maxApiVersion = null)
        {
            var provider = await _requestProvider.GetValueAsync(token).ConfigureAwait(false);
            await EnsureApiVersionAsync(provider, token, minApiLevel, minApiVersion, maxApiVersion);

            var (uri, content) = builder(provider);
            using (content)
            {
                using (var response = await _client.PostAsync(uri, content, token).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCodeEx();
                }
            }
        }

        // qBittorrent 4.x and 5.0.x report a rejected request as 200 with a "Fails." body
        private Task PostRejectingFailsBodyAsync(Func<IRequestProvider, (Uri, HttpContent)> builder,
            CancellationToken token)
        {
            return PostAsync(builder, token, async response =>
            {
                string body = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                if (string.Equals(body.Trim(), "Fails.", StringComparison.Ordinal))
                {
                    throw new QBittorrentClientRequestException("qBittorrent rejected the request.", HttpStatusCode.Forbidden);
                }

                return true;
            });
        }

        private async Task<T> PostAsync<T>(Func<IRequestProvider, (Uri, HttpContent)> builder,
            CancellationToken token,
            Func<HttpResponseMessage, Task<T>> transform,
            ApiLevel minApiLevel = ApiLevel.V1,
            ApiVersion minApiVersion = default)
        {
            var provider = await _requestProvider.GetValueAsync(token).ConfigureAwait(false);
            await EnsureApiVersionAsync(provider, token, minApiLevel, minApiVersion);

            var (uri, content) = builder(provider);
            using (content)
            {
                using (var response = await _client.PostAsync(uri, content, token).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCodeEx();
                    return await transform(response).ConfigureAwait(false);
                }
            }
        }

        private async Task<T> GetJsonAsync<T>(
            Func<IUrlProvider, Uri> builder,
            CancellationToken token,
            ApiLevel minApiLevel = ApiLevel.V1,
            ApiVersion minApiVersion = default)
        {
            var provider = await _requestProvider.GetValueAsync(token).ConfigureAwait(false);
            await EnsureApiVersionAsync(provider, token, minApiLevel, minApiVersion).ConfigureAwait(false);

            var uri = builder(provider.Url);
            var json = await _client.GetStringWithCancellationAsync(uri, token).ConfigureAwait(false);
            var result = JsonConvert.DeserializeObject<T>(json);
            return result;
        }

        private async Task<TResult> GetJsonAsync<T, TResult>(
            Func<IUrlProvider, Uri> builder,
            CancellationToken token,
            Func<T, TResult> transform,
            ApiLevel minApiLevel = ApiLevel.V1,
            ApiVersion minApiVersion = default)
        {
            var provider = await _requestProvider.GetValueAsync(token).ConfigureAwait(false);
            await EnsureApiVersionAsync(provider, token, minApiLevel, minApiVersion).ConfigureAwait(false);

            var uri = builder(provider.Url);
            var json = await _client.GetStringWithCancellationAsync(uri, token).ConfigureAwait(false);
            var result = JsonConvert.DeserializeObject<T>(json);
            return transform(result);
        }

        private async Task EnsureApiVersionAsync(IRequestProvider provider,
            CancellationToken token,
            ApiLevel minApiLevel = ApiLevel.V1,
            ApiVersion minApiVersion = default,
            ApiVersion? maxApiVersion = null)
        {
            if (minApiLevel != ApiLevel.V1 && provider.ApiLevel < minApiLevel)
                throw new ApiNotSupportedException(minApiLevel, minApiVersion != default ? (Version)minApiVersion : null);
            if (minApiVersion != default && await GetApiVersionAsync(token).ConfigureAwait(false) < minApiVersion)
                throw new ApiNotSupportedException(minApiLevel, minApiVersion);
            if (maxApiVersion != null && await GetApiVersionAsync(token).ConfigureAwait(false) > maxApiVersion)
                throw new ApiNotSupportedException(minApiLevel, minApiVersion, maxApiVersion);
        }

        private static async Task<IReadOnlyDictionary<string, long?>> GetTorrentLimits(HttpResponseMessage response, CancellationToken token)
        {
            var json = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            var dict = JsonConvert.DeserializeObject<Dictionary<string, long?>>(json, new NegativeToNullConverter());
            return dict;
        }

        private struct UrlItem
        {
            [JsonProperty("url")]
            public Uri Url { get; set; }
        }
    }
}
