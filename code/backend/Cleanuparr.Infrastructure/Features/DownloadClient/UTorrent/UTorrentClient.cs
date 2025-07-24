using Cleanuparr.Domain.Entities.UTorrent.Request;
using Cleanuparr.Domain.Entities.UTorrent.Response;
using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Persistence.Models.Configuration;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;

public sealed class UTorrentClient
{
    private readonly DownloadClientConfig _config;
    private readonly IUTorrentAuthenticator _authenticator;
    private readonly IUTorrentHttpService _httpService;
    private readonly IUTorrentResponseParser _responseParser;
    private readonly ILogger<UTorrentClient> _logger;

    public UTorrentClient(
        DownloadClientConfig config,
        IUTorrentAuthenticator authenticator,
        IUTorrentHttpService httpService,
        IUTorrentResponseParser responseParser,
        ILogger<UTorrentClient> logger
    )
    {
        _config = config;
        _authenticator = authenticator;
        _httpService = httpService;
        _responseParser = responseParser;
        _logger = logger;
    }

    /// <summary>
    /// Authenticates with µTorrent and retrieves the authentication token
    /// </summary>
    /// <returns>True if authentication was successful</returns>
    public async Task<bool> LoginAsync()
    {
        try
        {
            // Use the cache-aware authentication
            var token = await _authenticator.GetValidTokenAsync();
            return !string.IsNullOrEmpty(token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed for µTorrent client '{ClientName}'", _config.Name);
            throw new UTorrentException($"Failed to authenticate with µTorrent: {ex.Message}", ex);
        }
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
    /// Gets all torrents from µTorrent
    /// </summary>
    /// <returns>List of torrents</returns>
    public async Task<List<UTorrentItem>> GetTorrentsAsync()
    {
        try
        {
            var request = UTorrentRequestFactory.CreateTorrentListRequest();
            var json = await SendAuthenticatedRequestAsync(request);
            var response = _responseParser.ParseTorrentList(json);
            return response.Torrents;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get torrents from µTorrent client '{ClientName}'", _config.Name);
            throw new UTorrentException($"Failed to get torrents: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets a specific torrent by hash
    /// </summary>
    /// <param name="hash">Torrent hash</param>
    /// <returns>The torrent or null if not found</returns>
    public async Task<UTorrentItem?> GetTorrentAsync(string hash)
    {
        try
        {
            var torrents = await GetTorrentsAsync();
            return torrents.FirstOrDefault(t => 
                string.Equals(t.Hash, hash, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get torrent {Hash} from µTorrent client '{ClientName}'", hash, _config.Name);
            throw new UTorrentException($"Failed to get torrent {hash}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets files for a specific torrent
    /// </summary>
    /// <param name="hash">Torrent hash</param>
    /// <returns>List of files in the torrent</returns>
    public async Task<List<UTorrentFile>?> GetTorrentFilesAsync(string hash)
    {
        try
        {
            var request = UTorrentRequestFactory.CreateFileListRequest(hash);
            var json = await SendAuthenticatedRequestAsync(request);
            var response = _responseParser.ParseFileList(json);
            return response.Files;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get files for torrent {Hash} from µTorrent client '{ClientName}'", hash, _config.Name);
            throw new UTorrentException($"Failed to get files for torrent {hash}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets torrent properties including private/public status
    /// </summary>
    /// <param name="hash">Torrent hash</param>
    /// <returns>UTorrentProperties object or null if not found</returns>
    public async Task<UTorrentProperties> GetTorrentPropertiesAsync(string hash)
    {
        try
        {
            var request = UTorrentRequestFactory.CreatePropertiesRequest(hash);
            var json = await SendAuthenticatedRequestAsync(request);
            var response = _responseParser.ParseProperties(json);
            return response.Properties;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get properties for torrent {Hash} from µTorrent client '{ClientName}'", hash, _config.Name);
            throw new UTorrentException($"Failed to get properties for torrent {hash}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets all labels from µTorrent
    /// </summary>
    /// <returns>List of label names</returns>
    public async Task<List<string>> GetLabelsAsync()
    {
        try
        {
            var request = UTorrentRequestFactory.CreateLabelListRequest();
            var json = await SendAuthenticatedRequestAsync(request);
            var response = _responseParser.ParseLabelList(json);
            return response.Labels;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get labels from µTorrent client '{ClientName}'", _config.Name);
            throw new UTorrentException($"Failed to get labels: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Sets the label for a torrent
    /// </summary>
    /// <param name="hash">Torrent hash</param>
    /// <param name="label">Label to set</param>
    public async Task SetTorrentLabelAsync(string hash, string label)
    {
        try
        {
            var request = UTorrentRequestFactory.CreateSetLabelRequest(hash, label);
            await SendAuthenticatedRequestAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set label '{Label}' for torrent {Hash} in µTorrent client '{ClientName}'", label, hash, _config.Name);
            throw new UTorrentException($"Failed to set label '{label}' for torrent {hash}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Sets file priorities for a torrent
    /// </summary>
    /// <param name="hash">Torrent hash</param>
    /// <param name="fileIndexes">Index of the file to set priority for</param>
    /// <param name="priority">File priority (0=skip, 1=low, 2=normal, 3=high)</param>
    public async Task SetFilesPriorityAsync(string hash, List<int> fileIndexes, int priority)
    {
        try
        {
            var request = UTorrentRequestFactory.CreateSetFilePrioritiesRequest(hash, fileIndexes, priority);
            await SendAuthenticatedRequestAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set file priority for torrent {Hash} in µTorrent client '{ClientName}'", hash, _config.Name);
            throw new UTorrentException($"Failed to set file priority for torrent {hash}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Removes torrents from µTorrent
    /// </summary>
    /// <param name="hashes">List of torrent hashes to remove</param>
    public async Task RemoveTorrentsAsync(List<string> hashes)
    {
        try
        {
            foreach (var hash in hashes)
            {
                var request = UTorrentRequestFactory.CreateRemoveTorrentWithDataRequest(hash);
                await SendAuthenticatedRequestAsync(request);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove torrents from µTorrent client '{ClientName}'", _config.Name);
            throw new UTorrentException($"Failed to remove torrents: {ex.Message}", ex);
        }
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

    /// <summary>
    /// Sends an authenticated request to the µTorrent API
    /// Handles automatic authentication and retry logic
    /// </summary>
    /// <param name="request">The request to send</param>
    /// <returns>Raw JSON response from the API</returns>
    private async Task<string> SendAuthenticatedRequestAsync(UTorrentRequest request)
    {
        try
        {
            // Get valid token and cookie from cache-aware authenticator
            var token = await _authenticator.GetValidTokenAsync();
            var guidCookie = await _authenticator.GetValidGuidCookieAsync();
            
            request.Token = token;
            
            return await _httpService.SendRawRequestAsync(request, guidCookie);
        }
        catch (UTorrentAuthenticationException)
        {
            // On authentication failure, invalidate cache and retry once
            try
            {
                await _authenticator.InvalidateSessionAsync();
                var token = await _authenticator.GetValidTokenAsync();
                var guidCookie = await _authenticator.GetValidGuidCookieAsync();
                
                request.Token = token;
                
                return await _httpService.SendRawRequestAsync(request, guidCookie);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Authentication retry failed for µTorrent client '{ClientName}'", _config.Name);
                throw new UTorrentAuthenticationException($"Authentication retry failed: {ex.Message}", ex);
            }
        }
    }
}
