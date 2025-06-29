using System.Net.Http.Headers;
using Cleanuparr.Domain.Entities.Deluge.Request;
using Cleanuparr.Domain.Entities.Deluge.Response;
using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Infrastructure.Features.DownloadClient.Deluge.Extensions;
using Cleanuparr.Persistence.Models.Configuration;
using Newtonsoft.Json;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Deluge;

public sealed class DelugeClient
{
    private readonly DownloadClientConfig _config;
    private readonly HttpClient _httpClient;
    
    private static readonly IReadOnlyList<string> Fields =
    [
        "hash",
        "state",
        "name",
        "eta",
        "private",
        "total_done",
        "label",
        "seeding_time",
        "ratio",
        "trackers",
        "download_payload_rate",
        "total_size",
        "download_location"
    ];
    
    public DelugeClient(DownloadClientConfig config, HttpClient httpClient)
    {
        _config = config;
        _httpClient = httpClient;
    }
    
    public async Task<bool> LoginAsync()
    {
        return await SendRequest<bool>("auth.login", _config.Password);
    }

    public async Task<bool> IsConnected()
    {
        return await SendRequest<bool>("web.connected");
    }

    public async Task<bool> Connect()
    {
        string? firstHost = await GetHost();

        if (string.IsNullOrEmpty(firstHost))
        {
            return false;
        }

        var result = await SendRequest<List<string>?>("web.connect", firstHost);
        
        return result?.Count > 0;
    }

    public async Task<bool> Logout()
    {
        return await SendRequest<bool>("auth.delete_session");
    }

    public async Task<string?> GetHost()
    {
        var hosts = await SendRequest<List<List<string>?>?>("web.get_hosts");

        if (hosts?.Count > 1)
        {
            throw new FatalException("multiple Deluge hosts found - please connect to only one host");
        }
        
        return hosts?.FirstOrDefault()?.FirstOrDefault();
    }

    public async Task<List<DelugeTorrent>> ListTorrents(Dictionary<string, string>? filters = null)
    {
        filters ??= new Dictionary<string, string>();
        var keys = typeof(DelugeTorrent).GetAllJsonPropertyFromType();
        Dictionary<string, DelugeTorrent> result =
            await SendRequest<Dictionary<string, DelugeTorrent>>("core.get_torrents_status", filters, keys);
        return result.Values.ToList();
    }

    public async Task<List<DelugeTorrentExtended>> ListTorrentsExtended(Dictionary<string, string>? filters = null)
    {
        filters ??= new Dictionary<string, string>();
        var keys = typeof(DelugeTorrentExtended).GetAllJsonPropertyFromType();
        Dictionary<string, DelugeTorrentExtended> result =
            await SendRequest<Dictionary<string, DelugeTorrentExtended>>("core.get_torrents_status", filters, keys);
        return result.Values.ToList();
    }

    public async Task<DelugeTorrent?> GetTorrent(string hash)
    {
        List<DelugeTorrent> torrents = await ListTorrents(new Dictionary<string, string>() { { "hash", hash } });
        return torrents.FirstOrDefault();
    }

    public async Task<DelugeTorrentExtended?> GetTorrentExtended(string hash)
    {
        List<DelugeTorrentExtended> torrents =
            await ListTorrentsExtended(new Dictionary<string, string> { { "hash", hash } });
        return torrents.FirstOrDefault();
    }
    
    public async Task<DownloadStatus?> GetTorrentStatus(string hash)
    {
        try
        {
            return await SendRequest<DownloadStatus?>(
                "web.get_torrent_status",
                hash,
                Fields
            );
        }
        catch (DelugeClientException e)
        {
            // Deluge returns an error when the torrent is not found
            if (e.Message == "AttributeError: 'NoneType' object has no attribute 'call'")
            {
                return null;
            }

            throw;
        }
    }
    
    public async Task<List<DownloadStatus>?> GetStatusForAllTorrents()
    {
        Dictionary<string, DownloadStatus>? downloads = await SendRequest<Dictionary<string, DownloadStatus>?>(
            "core.get_torrents_status",
            "",
            Fields
        );
        
        return downloads?.Values.ToList();
    }

    public async Task<DelugeContents?> GetTorrentFiles(string hash)
    {
        return await SendRequest<DelugeContents?>("web.get_torrent_files", hash);
    }

    public async Task ChangeFilesPriority(string hash, List<int> priorities)
    {
        Dictionary<string, List<int>> filePriorities = new()
        {
            { "file_priorities", priorities }
        };

        await SendRequest<DelugeResponse<object>>("core.set_torrent_options", hash, filePriorities);
    }

    public async Task DeleteTorrents(List<string> hashes)
    {
        await SendRequest<DelugeResponse<object>>("core.remove_torrents", hashes, true);
    }

    private async Task<String> PostJson(String json)
    {
        StringContent content = new StringContent(json);
        content.Headers.ContentType = new MediaTypeWithQualityHeaderValue("application/json");
        
        UriBuilder uriBuilder = new(_config.Url);
        uriBuilder.Path = $"{uriBuilder.Path.TrimEnd('/')}/json";
        var responseMessage = await _httpClient.PostAsync(uriBuilder.Uri, content);
        responseMessage.EnsureSuccessStatusCode();

        var responseJson = await responseMessage.Content.ReadAsStringAsync();
        return responseJson;
    }

    private static DelugeRequest CreateRequest(string method, params object[] parameters)
    {
        if (String.IsNullOrWhiteSpace(method))
        {
            throw new ArgumentException(nameof(method));
        }
        
        return new DelugeRequest(1, method, parameters);
    }
    
    public async Task<T> SendRequest<T>(string method, params object[] parameters)
    {
        return await SendRequest<T>(CreateRequest(method, parameters));
    }

    public async Task<T> SendRequest<T>(DelugeRequest webRequest)
    {
        var requestJson = JsonConvert.SerializeObject(webRequest, Formatting.None, new JsonSerializerSettings
        {
            NullValueHandling = webRequest.NullValueHandling
        });

        var responseJson = await PostJson(requestJson);
        var settings = new JsonSerializerSettings
        {
            Error = (_, args) =>
            {
                // Suppress the error and continue
                args.ErrorContext.Handled = true;
            }
        };
        
        DelugeResponse<T>? webResponse = JsonConvert.DeserializeObject<DelugeResponse<T>>(responseJson, settings);

        if (webResponse?.Error != null)
        {
            throw new DelugeClientException(webResponse.Error.Message);
        }

        if (webResponse?.ResponseId != webRequest.RequestId)
        {
            throw new DelugeClientException("desync");
        }

        return webResponse.Result;
    }

    public async Task<IReadOnlyList<string>> GetLabels()
    {
        return await SendRequest<IReadOnlyList<string>>("label.get_labels");
    }
    
    public async Task CreateLabel(string label)
    {
        await SendRequest<DelugeResponse<object>>("label.add", label);
    }

    public async Task SetTorrentLabel(string hash, string newLabel)
    {
        await SendRequest<DelugeResponse<object>>("label.set_torrent", hash, newLabel);
    }
}