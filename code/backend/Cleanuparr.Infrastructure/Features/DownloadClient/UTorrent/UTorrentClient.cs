using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using Cleanuparr.Domain.Entities.UTorrent.Request;
using Cleanuparr.Domain.Entities.UTorrent.Response;
using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent.Extensions;
using Cleanuparr.Persistence.Models.Configuration;
using Newtonsoft.Json;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;

/// <summary>
/// Direct client for communicating with µTorrent Web UI API
/// Handles authentication, token management, and raw API calls
/// </summary>
public sealed class UTorrentClient
{
    private readonly DownloadClientConfig _config;
    private readonly HttpClient _httpClient;
    private readonly object _authLock = new();
    
    private static string _authToken = string.Empty;
    private static string _guidCookie = string.Empty;

    // Regex pattern to extract token from µTorrent Web UI HTML (working pattern from test)
    private static readonly Regex TokenRegex = new(@"<div[^>]*id=['""]token['""][^>]*>([^<]+)</div>", 
        RegexOptions.IgnoreCase);

    public UTorrentClient(DownloadClientConfig config, HttpClient httpClient)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// Authenticates with µTorrent and retrieves the authentication token
    /// </summary>
    /// <returns>True if authentication was successful</returns>
    public async Task<bool> LoginAsync()
    {
        try
        {
            var (token, guidCookie) = await GetTokenAndCookieAsync();
            
            lock (_authLock)
            {
                _authToken = token;
                _guidCookie = guidCookie;
            }

            return true;
        }
        catch (Exception ex)
        {
            throw new UTorrentException($"Failed to authenticate with µTorrent: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Retrieves both token and GUID cookie from µTorrent Web UI
    /// </summary>
    /// <returns>Tuple of (token, guidCookie)</returns>
    private async Task<(string token, string guidCookie)> GetTokenAndCookieAsync()
    {
        var tokenUrl = $"{_config.Url.ToString().TrimEnd('/')}/gui/token.html";
        var request = new HttpRequestMessage(HttpMethod.Get, tokenUrl);

        // Add basic authentication if configured
        if (!string.IsNullOrEmpty(_config.Username) && !string.IsNullOrEmpty(_config.Password))
        {
            var authString = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_config.Username}:{_config.Password}"));
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authString);
        }

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Failed to get token. Status: {response.StatusCode}");
        }

        var html = await response.Content.ReadAsStringAsync();

        // Extract token from HTML using working pattern
        var tokenMatch = TokenRegex.Match(html);
        if (!tokenMatch.Success)
        {
            throw new Exception($"Could not extract token from HTML: {html}");
        }

        var token = tokenMatch.Groups[1].Value.Trim();

        // Extract GUID cookie specifically
        var guidCookie = "";
        if (response.Headers.Contains("Set-Cookie"))
        {
            var cookies = response.Headers.GetValues("Set-Cookie");
            var guidCookieHeader = cookies.FirstOrDefault(c => c.StartsWith("GUID="));
            if (guidCookieHeader != null)
            {
                guidCookie = guidCookieHeader.Split(';')[0]; // Get just the GUID=value part
            }
        }

        if (string.IsNullOrEmpty(guidCookie))
        {
            guidCookie = _guidCookie;
        }

        return (token, guidCookie);
    }

    /// <summary>
    /// Ensures we have a valid authentication token and GUID cookie
    /// </summary>
    private async Task EnsureAuthenticatedAsync()
    {
        lock (_authLock)
        {
            if (!string.IsNullOrEmpty(_authToken) && !string.IsNullOrEmpty(_guidCookie))
            {
                return;
            }
        }

        // If we don't have both token and GUID cookie, try to login
        await LoginAsync();
    }

    /// <summary>
    /// Refreshes the authentication session (token and GUID cookie)
    /// </summary>
    public async Task RefreshSessionAsync()
    {
        await LoginAsync();
    }

    /// <summary>
    /// Validates that we have proper authentication credentials
    /// </summary>
    private void ValidateAuthentication()
    {
        lock (_authLock)
        {
            if (string.IsNullOrEmpty(_authToken))
            {
                throw new InvalidOperationException("Authentication token is missing. Please call LoginAsync() first.");
            }
            
            if (string.IsNullOrEmpty(_guidCookie))
            {
                throw new InvalidOperationException("GUID cookie is missing. Please call LoginAsync() first.");
            }
        }
    }

    /// <summary>
    /// Sends a request to the µTorrent Web UI API
    /// </summary>
    /// <typeparam name="T">Expected response type</typeparam>
    /// <param name="request">The request to send</param>
    /// <returns>The parsed response</returns>
    public async Task<T?> SendRequestAsync<T>(UTorrentRequest request)
    {
        await EnsureAuthenticatedAsync();
        ValidateAuthentication();

        string token;
        string guidCookie;
        lock (_authLock)
        {
            token = _authToken;
            guidCookie = _guidCookie;
        }

        request.Token = token;

        try
        {
            return await SendRequestInternalAsync<T>(request, guidCookie);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized || 
                                            ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            // Token might be expired, try to refresh session and retry once
            await RefreshSessionAsync();
            ValidateAuthentication();
            
            lock (_authLock)
            {
                token = _authToken!;
                guidCookie = _guidCookie!;
            }
            request.Token = token;
            
            return await SendRequestInternalAsync<T>(request, guidCookie);
        }
    }

    /// <summary>
    /// Internal method to send a request to µTorrent
    /// </summary>
    private async Task<T?> SendRequestInternalAsync<T>(UTorrentRequest request, string guidCookie)
    {
        try
        {
            var queryString = request.ToQueryString();
            var requestUrl = $"{_config.Url.ToString().TrimEnd('/')}/gui/?{queryString}";
            
            HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            
            // Add basic authentication if configured
            if (!string.IsNullOrEmpty(_config.Username) && !string.IsNullOrEmpty(_config.Password))
            {
                var authString = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_config.Username}:{_config.Password}"));
                httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authString);
            }

            // Add the GUID cookie (required for authentication)
            httpRequest.Headers.Add("Cookie", guidCookie);

            var response = await _httpClient.SendAsync(httpRequest);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();

            if (typeof(T) == typeof(string))
            {
                return (T)(object)content;
            }

            var result = JsonConvert.DeserializeObject<T>(content);
            return result;
        }
        catch (HttpRequestException ex)
        {
            throw new UTorrentException($"HTTP error during µTorrent API call: {ex.Message}", ex);
        }
        catch (JsonException ex)
        {
            throw new UTorrentException($"Failed to parse µTorrent API response: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets all torrents from µTorrent
    /// </summary>
    /// <returns>List of torrents</returns>
    public async Task<List<UTorrentItem>> GetTorrentsAsync()
    {
        var request = UTorrentRequest.Create("list=1", string.Empty);
        var response = await SendRequestAsync<UTorrentResponse<object[][]>>(request);

        if (response?.Torrents == null)
        {
            return new List<UTorrentItem>();
        }

        var torrents = new List<UTorrentItem>();
        foreach (var data in response.Torrents)
        {
            if (data is { Length: >= 27 })
            {
                // Accept arrays with 27 or more elements (µTorrent now returns 29 elements)
                torrents.Add(new UTorrentItem
                {
                    Hash = data[0]?.ToString() ?? string.Empty,
                    Status = Convert.ToInt32(data[1]),
                    Name = data[2]?.ToString() ?? string.Empty,
                    Size = Convert.ToInt64(data[3]),
                    Progress = Convert.ToInt32(data[4]),
                    Downloaded = Convert.ToInt64(data[5]),
                    Uploaded = Convert.ToInt64(data[6]),
                    RatioRaw = Convert.ToInt32(data[7]),
                    UploadSpeed = Convert.ToInt32(data[8]),
                    DownloadSpeed = Convert.ToInt32(data[9]),
                    ETA = Convert.ToInt32(data[10]),
                    Label = data[11]?.ToString() ?? string.Empty,
                    PeersConnected = Convert.ToInt32(data[12]),
                    PeersInSwarm = Convert.ToInt32(data[13]),
                    SeedsConnected = Convert.ToInt32(data[14]),
                    SeedsInSwarm = Convert.ToInt32(data[15]),
                    Availability = Convert.ToInt32(data[16]),
                    QueueOrder = Convert.ToInt32(data[17]),
                    Remaining = Convert.ToInt64(data[18]),
                    DownloadUrl = data[19]?.ToString() ?? string.Empty,
                    RssFeedUrl = data[20]?.ToString() ?? string.Empty,
                    StatusMessage = data[21]?.ToString() ?? string.Empty,
                    StreamId = data[22]?.ToString() ?? string.Empty,
                    DateAdded = Convert.ToInt64(data[23]),
                    DateCompleted = Convert.ToInt64(data[24]),
                    AppUpdateUrl = data[25]?.ToString() ?? string.Empty,
                    SavePath = data[26]?.ToString() ?? string.Empty
                });
            }
        }

        return torrents;
    }

    /// <summary>
    /// Tests the authentication and basic API connectivity
    /// </summary>
    /// <returns>True if authentication and basic API call works</returns>
    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var torrents = await GetTorrentsAsync();
            return true; // If we can get torrents, authentication is working
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets a specific torrent by hash
    /// </summary>
    /// <param name="hash">Torrent hash</param>
    /// <returns>The torrent or null if not found</returns>
    public async Task<UTorrentItem?> GetTorrentAsync(string hash)
    {
        var torrents = await GetTorrentsAsync();
        return torrents.FirstOrDefault(t => 
            string.Equals(t.Hash, hash, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets files for a specific torrent
    /// </summary>
    /// <param name="hash">Torrent hash</param>
    /// <returns>List of files in the torrent</returns>
    public async Task<List<UTorrentFile>?> GetTorrentFilesAsync(string hash)
    {
        var request = UTorrentRequest.Create("action=getfiles", string.Empty)
            .WithParameter("hash", hash);

        var response = await SendRequestAsync<UTorrentResponse<object>>(request);

        return response?.Files;
    }

    /// <summary>
    /// Sets file priorities for a torrent
    /// </summary>
    /// <param name="hash">Torrent hash</param>
    /// <param name="priorities">Array of priorities (0=skip, 1=low, 2=normal, 3=high)</param>
    public async Task SetFilePrioritiesAsync(string hash, int[] priorities)
    {
        var request = UTorrentRequest.Create("setprio", string.Empty)
            .WithParameter("hash", hash);

        // Add each priority as a separate parameter
        for (int i = 0; i < priorities.Length; i++)
        {
            request.WithParameter($"f{i}", priorities[i].ToString());
        }

        await SendRequestAsync<UTorrentResponse<object>>(request);
    }

    /// <summary>
    /// Removes torrents from µTorrent
    /// </summary>
    /// <param name="hashes">List of torrent hashes to remove</param>
    /// <param name="deleteData">Whether to delete the data files as well</param>
    public async Task RemoveTorrentsAsync(List<string> hashes, bool deleteData = false)
    {
        var action = deleteData ? "removedata" : "remove";

        foreach (var hash in hashes)
        {
            var request = UTorrentRequest.Create(action, string.Empty)
                .WithParameter("hash", hash);

            await SendRequestAsync<UTorrentResponse<object>>(request);
        }
    }

    /// <summary>
    /// Gets all labels from µTorrent
    /// </summary>
    /// <returns>List of label names</returns>
    public async Task<List<string>> GetLabelsAsync()
    {
        var request = UTorrentRequest.Create("list=1", string.Empty);
        var response = await SendRequestAsync<UTorrentResponse<object[][]>>(request);

        if (response?.Labels == null)
        {
            return new List<string>();
        }

        var labels = new List<string>();
        foreach (var labelData in response.Labels)
        {
            if (labelData is { Length: > 0 })
            {
                var labelName = labelData[0]?.ToString();
                if (!string.IsNullOrEmpty(labelName))
                {
                    labels.Add(labelName);
                }
            }
        }

        return labels;
    }

    /// <summary>
    /// Sets the label for a torrent
    /// </summary>
    /// <param name="hash">Torrent hash</param>
    /// <param name="label">Label to set</param>
    public async Task SetTorrentLabelAsync(string hash, string label)
    {
        var request = UTorrentRequest.Create("setprops", string.Empty)
            .WithParameter("hash", hash)
            .WithParameter("s", "label")
            .WithParameter("v", label);

        await SendRequestAsync<UTorrentResponse<object>>(request);
    }

    /// <summary>
    /// Starts a torrent
    /// </summary>
    /// <param name="hash">Torrent hash</param>
    public async Task StartTorrentAsync(string hash)
    {
        var request = UTorrentRequest.Create("start", string.Empty)
            .WithParameter("hash", hash);

        await SendRequestAsync<UTorrentResponse<object>>(request);
    }

    /// <summary>
    /// Stops a torrent
    /// </summary>
    /// <param name="hash">Torrent hash</param>
    public async Task StopTorrentAsync(string hash)
    {
        var request = UTorrentRequest.Create("stop", string.Empty)
            .WithParameter("hash", hash);

        await SendRequestAsync<UTorrentResponse<object>>(request);
    }

    /// <summary>
    /// Pauses a torrent
    /// </summary>
    /// <param name="hash">Torrent hash</param>
    public async Task PauseTorrentAsync(string hash)
    {
        var request = UTorrentRequest.Create("pause", string.Empty)
            .WithParameter("hash", hash);

        await SendRequestAsync<UTorrentResponse<object>>(request);
    }

    /// <summary>
    /// Forces a torrent to start
    /// </summary>
    /// <param name="hash">Torrent hash</param>
    public async Task ForceStartTorrentAsync(string hash)
    {
        var request = UTorrentRequest.Create("forcestart", string.Empty)
            .WithParameter("hash", hash);

        await SendRequestAsync<UTorrentResponse<object>>(request);
    }

    /// <summary>
    /// Gets torrent properties including private/public status
    /// </summary>
    /// <param name="hash">Torrent hash</param>
    /// <returns>UTorrentProperties object or null if not found</returns>
    public async Task<UTorrentProperties> GetTorrentPropertiesAsync(string hash)
    {
        var request = UTorrentRequest.Create("action=getprops", string.Empty)
            .WithParameter("hash", hash);

        var response = await SendRequestAsync<UTorrentResponse<object[][]>>(request);

        if (response?.Properties == null || response.Properties.Length == 0)
        {
            throw new UTorrentException($"No properties found for torrent with hash {hash}");
        }

        return response.Properties.First();
    }

    /// <summary>
    /// Creates a new label in µTorrent
    /// </summary>
    /// <param name="label">Label name to create</param>
    public static async Task CreateLabel(string label)
    {
        // µTorrent doesn't have an explicit "create label" API
        // Labels are created automatically when you assign them to a torrent
        // So this is a no-op for µTorrent
        await Task.CompletedTask;
    }
} 