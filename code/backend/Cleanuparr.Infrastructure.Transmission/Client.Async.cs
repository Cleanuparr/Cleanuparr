using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Transmission.API.RPC.Entity;
using Transmission.API.RPC.Common;
using Transmission.API.RPC.Arguments;

namespace Transmission.API.RPC
{
    public partial class Client
    {
        #region Session methods

        /// <summary>
        /// Close current session (API: session-close)
        /// </summary>
        public async Task CloseSessionAsync()
        {
            var request = new TransmissionRequest("session-close");
            await SendRequestAsync(request);
        }

        /// <summary>
        /// Set information to current session (API: session-set)
        /// </summary>
        /// <param name="settings">New session settings</param>
        public async Task SetSessionSettingsAsync(SessionSettings settings)
        {
            var request = new TransmissionRequest("session-set", settings);
            await SendRequestAsync(request);
        }

        /// <summary>
        /// Get session stat
        /// </summary>
        /// <returns>Session stat</returns>
        public async Task<Statistic?> GetSessionStatisticAsync()
        {
            var request = new TransmissionRequest("session-stats");
            var response = await SendRequestAsync(request);
            var result = response.Arguments.Deserialize<Statistic>(_jsonOptions);
            return result;
        }

        /// <summary>
        /// Get information of current session (API: session-get)
        /// </summary>
        /// <returns>Session information</returns>
        public async Task<SessionInfo?> GetSessionInformationAsync()
        {
            var request = new TransmissionRequest("session-get");
            var response = await SendRequestAsync(request);
            var result = response.Arguments.Deserialize<SessionInfo>(_jsonOptions);
            return result;
        }



        /// <summary>
        /// Get information of current session (API: session-get)
        /// </summary>
        /// <param name="fields">Optional fields of session information</param>
        /// <returns>Session information</returns>
        public async Task<SessionInfo?> GetSessionInformationAsync(string[] fields)
        {
            var arguments = new Dictionary<string, object?>();
            arguments.Add("fields", fields);

            var request = new TransmissionRequest("session-get", arguments);
            var response = await SendRequestAsync(request);
            var result = response.Arguments.Deserialize<SessionInfo>(_jsonOptions);
            return result;
        }
        #endregion

        #region Torrents methods

        /// <summary>
        /// Add torrent (API: torrent-add)
        /// </summary>
        /// <returns>Torrent info (ID, Name and HashString)</returns>
        public async Task<NewTorrentInfo?> TorrentAddAsync(NewTorrent torrent)
        {
            if (String.IsNullOrWhiteSpace(torrent.Metainfo) && String.IsNullOrWhiteSpace(torrent.Filename))
                throw new ArgumentException("Either \"filename\" or \"metainfo\" must be included.");

            var request = new TransmissionRequest("torrent-add", torrent);
            var response = await SendRequestAsync(request);
            NewTorrentInfo? result = null;

            if (response.Arguments.TryGetProperty("torrent-duplicate", out JsonElement value))
            {
                result = value.Deserialize<NewTorrentInfo>(_jsonOptions);
                if (result != null) result.Duplicate = true;
            }
            else if (response.Arguments.TryGetProperty("torrent-added", out value))
            {
                result = value.Deserialize<NewTorrentInfo>(_jsonOptions);
                if (result != null) result.Duplicate = false;
            }

            return result;
        }

        /// <summary>
        /// Set torrent params (API: torrent-set)
        /// </summary>
        /// <param name="settings">Torrent settings</param>
        public async Task TorrentSetAsync(TorrentSettings settings)
        {
            var request = new TransmissionRequest("torrent-set", settings);
            await SendRequestAsync(request);
        }

        /// <summary>
        /// Get fields of recently active torrents (API: torrent-get)
        /// </summary>
        /// <param name="fields">Fields of torrents</param>
        /// <returns>Torrents info</returns>
        public async Task<TransmissionTorrents?> TorrentGetRecentlyActiveAsync(string[] fields)
        {
            var arguments = new Dictionary<string, object?>();
            arguments.Add("fields", fields);
            arguments.Add("ids", "recently-active");

            var request = new TransmissionRequest("torrent-get", arguments);
            var response = await SendRequestAsync(request);


            TransmissionTorrents? torrents = response.Arguments.Deserialize<TransmissionTorrents>(_jsonOptions);

            return torrents;
        }

        /// <summary>
        /// Get fields of torrents from ids (API: torrent-get)
        /// </summary>
        /// <param name="fields">Fields of torrents</param>
        /// <param name="ids">IDs of torrents (null or empty for get all torrents)</param>
        /// <returns>Torrents info</returns>
        public async Task<TransmissionTorrents?> TorrentGetAsync(string[] fields, params string[] ids)
        {
            var arguments = new Dictionary<string, object?>();
            arguments.Add("fields", fields);

            if (ids != null && ids.Length > 0)
                arguments.Add("ids", ids);

            var request = new TransmissionRequest("torrent-get", arguments);
            var response = await SendRequestAsync(request);


            TransmissionTorrents? torrents = response.Arguments.Deserialize<TransmissionTorrents>(_jsonOptions);

            return torrents;
        }

        /// <summary>
        /// Remove torrents
        /// </summary>
        /// <param name="ids">Torrents id</param>
        /// <param name="deleteData">Remove data</param>
        public async Task TorrentRemoveAsync(long[] ids, bool deleteData = false)
        {
            var arguments = new Dictionary<string, object?>();

            arguments.Add("ids", ids);
            arguments.Add("delete-local-data", deleteData);

            var request = new TransmissionRequest("torrent-remove", arguments);
            await SendRequestAsync(request);
        }

        #region Torrent Start

        /// <summary>
        /// Start torrents (API: torrent-start)
        /// </summary>
        /// <param name="ids">A list of torrent id numbers, sha1 hash strings, or both</param>
        public async Task TorrentStartAsync(object[] ids)
        {
            var request = new TransmissionRequest("torrent-start", new Dictionary<string, object?> { { "ids", ids } });
            await SendRequestAsync(request);
        }

        /// <summary>
        /// Start recently active torrents (API: torrent-start)
        /// </summary>
        public async Task TorrentStartRecentlyActiveAsync()
        {
            var request = new TransmissionRequest("torrent-start", new Dictionary<string, object?> { { "ids", "recently-active" } });
            await SendRequestAsync(request);
        }

        #endregion

        #region Torrent Start Now

        /// <summary>
        /// Start now torrents (API: torrent-start-now)
        /// </summary>
        /// <param name="ids">A list of torrent id numbers, sha1 hash strings, or both</param>
        public async Task TorrentStartNowAsync(object[] ids)
        {
            var request = new TransmissionRequest("torrent-start-now", new Dictionary<string, object?> { { "ids", ids } });
            await SendRequestAsync(request);
        }

        /// <summary>
        /// Start now recently active torrents (API: torrent-start-now)
        /// </summary>
        public async Task TorrentStartNowRecentlyActiveAsync()
        {
            var request = new TransmissionRequest("torrent-start-now", new Dictionary<string, object?> { { "ids", "recently-active" } });
            await SendRequestAsync(request);
        }

        #endregion

        #region Torrent Stop

        /// <summary>
        /// Stop torrents (API: torrent-stop)
        /// </summary>
        /// <param name="ids">A list of torrent id numbers, sha1 hash strings, or both</param>
        public async Task TorrentStopAsync(object[] ids)
        {
            var request = new TransmissionRequest("torrent-stop", new Dictionary<string, object?> { { "ids", ids } });
            await SendRequestAsync(request);
        }

        /// <summary>
        /// Stop recently active torrents (API: torrent-stop)
        /// </summary>
        public async Task TorrentStopRecentlyActiveAsync()
        {
            var request = new TransmissionRequest("torrent-stop", new Dictionary<string, object?> { { "ids", "recently-active" } });
            await SendRequestAsync(request);
        }

        #endregion

        #region Torrent Verify

        /// <summary>
        /// Verify torrents (API: torrent-verify)
        /// </summary>
        /// <param name="ids">A list of torrent id numbers, sha1 hash strings, or both</param>
        public async Task TorrentVerifyAsync(object[] ids)
        {
            var request = new TransmissionRequest("torrent-verify", new Dictionary<string, object?> { { "ids", ids } });
            await SendRequestAsync(request);
        }

        /// <summary>
        /// Verify recently active torrents (API: torrent-verify)
        /// </summary>
        public async Task TorrentVerifyRecentlyActiveAsync()
        {
            var request = new TransmissionRequest("torrent-verify", new Dictionary<string, object?> { { "ids", "recently-active" } });
            await SendRequestAsync(request);
        }

        #endregion

        #region Torrent Reannounce

        /// <summary>
        /// Reannounce torrents (API: torrent-reannounce)
        /// </summary>
        /// <param name="ids">A list of torrent id numbers, sha1 hash strings, or both</param>
        public async Task TorrentReannounceAsync(object[] ids)
        {
            var request = new TransmissionRequest("torrent-reannounce", new Dictionary<string, object?> { { "ids", ids } });
            await SendRequestAsync(request);
        }

        /// <summary>
        /// Reannounce recently active torrents (API: torrent-reannounce)
        /// </summary>
        public async Task TorrentReannounceRecentlyActiveAsync()
        {
            var request = new TransmissionRequest("torrent-reannounce", new Dictionary<string, object?> { { "ids", "recently-active" } });
            await SendRequestAsync(request);
        }

        #endregion

        #region Torrent Queue

        /// <summary>
        /// Move torrents in queue on top (API: queue-move-top)
        /// </summary>
        /// <param name="ids">Torrents id</param>
        public async Task TorrentQueueMoveTopAsync(long[] ids)
        {
            var request = new TransmissionRequest("queue-move-top", new Dictionary<string, object?> { { "ids", ids } });
            await SendRequestAsync(request);
        }

        /// <summary>
        /// Move up torrents in queue (API: queue-move-up)
        /// </summary>
        /// <param name="ids"></param>
        public async Task TorrentQueueMoveUpAsync(long[] ids)
        {
            var request = new TransmissionRequest("queue-move-up", new Dictionary<string, object?> { { "ids", ids } });
            await SendRequestAsync(request);
        }

        /// <summary>
        /// Move down torrents in queue (API: queue-move-down)
        /// </summary>
        /// <param name="ids"></param>
        public async Task TorrentQueueMoveDownAsync(long[] ids)
        {
            var request = new TransmissionRequest("queue-move-down", new Dictionary<string, object?> { { "ids", ids } });
            await SendRequestAsync(request);
        }

        /// <summary>
        /// Move torrents to bottom in queue  (API: queue-move-bottom)
        /// </summary>
        /// <param name="ids"></param>
        public async Task TorrentQueueMoveBottomAsync(long[] ids)
        {
            var request = new TransmissionRequest("queue-move-bottom", new Dictionary<string, object?> { { "ids", ids } });
            await SendRequestAsync(request);
        }

        #endregion

        /// <summary>
        /// Set new location for torrents files (API: torrent-set-location)
        /// </summary>
        /// <param name="ids">Torrent ids</param>
        /// <param name="location">The new torrent location</param>
        /// <param name="move">Move from previous location</param>
        public async Task TorrentSetLocationAsync(long[] ids, string location, bool move)
        {
            var arguments = new Dictionary<string, object?>();
            arguments.Add("ids", ids);
            arguments.Add("location", location);
            arguments.Add("move", move);

            var request = new TransmissionRequest("torrent-set-location", arguments);
            await SendRequestAsync(request);
        }

        /// <summary>
        /// Rename a file or directory in a torrent (API: torrent-rename-path)
        /// </summary>
        /// <param name="id">The torrent whose path will be renamed</param>
        /// <param name="path">The path to the file or folder that will be renamed</param>
        /// <param name="name">The file or folder's new name</param>
        public async Task<RenameTorrentInfo?> TorrentRenamePathAsync(long id, string path, string name)
        {
            var arguments = new Dictionary<string, object?>();
            arguments.Add("ids", new long[] { id });
            arguments.Add("path", path);
            arguments.Add("name", name);

            var request = new TransmissionRequest("torrent-rename-path", arguments);
            var response = await SendRequestAsync(request);

            var result = response.Arguments.Deserialize<RenameTorrentInfo>(_jsonOptions);

            return result;
        }

        #endregion

        #region Bandwidth Groups

        /// <summary>
        /// Get bandwidth groups (API: group-get)
        /// </summary>
        /// <returns></returns>
        public async Task<BandwidthGroup[]?> BandwidthGroupGetAsync()
        {
            var request = new TransmissionRequest("group-get");

            var response = await SendRequestAsync(request);
            var result = response.Arguments.GetProperty("group").Deserialize<BandwidthGroup[]>(_jsonOptions);

            return result;
        }

        /// <summary>
        /// Get bandwidth groups (API: group-get)
        /// </summary>
        /// <param name="groups">Optional names of groups to get</param>
        /// <returns></returns>
        public async Task<BandwidthGroup[]?> BandwidthGroupGetAsync(string[] groups)
        {
            var arguments = new Dictionary<string, object?>();
            arguments.Add("name", groups);

            var request = new TransmissionRequest("group-get", arguments);

            var response = await SendRequestAsync(request);
            var result = response.Arguments.GetProperty("group").Deserialize<BandwidthGroup[]>(_jsonOptions);

            return result;
        }

        /// <summary>
        /// Set bandwidth groups (API: group-set)
        /// </summary>
        /// <param name="group">A bandwidth group to set</param>
        public async Task BandwidthGroupSetAsync(BandwidthGroupSettings group)
        {
            var request = new TransmissionRequest("group-set", group);
            await SendRequestAsync(request);
        }

        #endregion

        #region System

        /// <summary>
        /// See if your incoming peer port is accessible from the outside world (API: port-test)
        /// </summary>
        /// <returns>A Tuple with a boolean of whether the port test succeeded, and a PortTestProtocol enum of which protocol was used for the test</returns>
        public async Task<Tuple<bool?, PortTestProtocol>> PortTestAsync()
        {
            var request = new TransmissionRequest("port-test");
            var response = await SendRequestAsync(request);

            bool? result = null;
            if (response.Arguments.TryGetProperty("port-is-open", out JsonElement isOpen))
            {
                result = isOpen.Deserialize<bool?>(_jsonOptions);
            }

            PortTestProtocol protocol = PortTestProtocol.Unknown;
            if (response.Arguments.TryGetProperty("ipProtocol", out JsonElement protocolValue) && protocolValue.ValueKind == JsonValueKind.String)
            {
                switch (protocolValue.GetString())
                {
                    case "ipv4": protocol = PortTestProtocol.IPv4; break;
                    case "ipv6": protocol = PortTestProtocol.IPV6; break;
                }
            }
            return new Tuple<bool?, PortTestProtocol>(result, protocol);
        }

        /// <summary>
        /// Update blocklist (API: blocklist-update)
        /// </summary>
        /// <returns>Blocklist size</returns>
        public async Task<long?> BlocklistUpdateAsync()
        {
            var request = new TransmissionRequest("blocklist-update");
            var response = await SendRequestAsync(request);

            if (response.Arguments.TryGetProperty("blocklist-size", out JsonElement size))
            {
                return size.Deserialize<long?>(_jsonOptions);
            }

            return null;
        }

        /// <summary>
        /// Get free space is available in a client-specified folder.
        /// </summary>
        /// <param name="path">The directory to query</param>
        public async Task<FreeSpace?> FreeSpaceAsync(string path)
        {
            var arguments = new Dictionary<string, object?>();
            arguments.Add("path", path);

            var request = new TransmissionRequest("free-space", arguments);
            var response = await SendRequestAsync(request);

            var data = response.Arguments.Deserialize<FreeSpace>(_jsonOptions);
            return data;
        }

        #endregion

        private async Task<TransmissionResponse> SendRequestAsync(TransmissionRequest request, bool isRetry = false)
        {
            TransmissionResponse result;

            request.Tag = ++CurrentTag;

            HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Post, Url);
            httpRequest.Headers.Add("X-Transmission-Session-Id", SessionID);

            if (_needAuthorization)
                httpRequest.Headers.Add("Authorization", _authorization);

            httpRequest.Content = new StringContent(request.ToJson(), Encoding.UTF8, "application/json-rpc");

            //Send request and prepare response
            using (var httpResponse = await _httpClient.SendAsync(httpRequest))
            {
                if (httpResponse.StatusCode == HttpStatusCode.Conflict)
                {
                    if (isRetry || !httpResponse.Headers.TryGetValues("X-Transmission-Session-Id", out var values))
                    {
                        throw new SessionIdException();
                    }

                    SessionID = values.First();
                    return await SendRequestAsync(request, true);
                }

                httpResponse.EnsureSuccessStatusCode();

                var responseString = await httpResponse.Content.ReadAsStringAsync();
                result = JsonSerializer.Deserialize<TransmissionResponse>(responseString, _jsonOptions)
                    ?? throw new RequestFailedException("Empty response");

                if (result.Result != "success")
                {
                    throw new RequestFailedException(result.Result);
                }
            }

            return result;
        }
    }
}
