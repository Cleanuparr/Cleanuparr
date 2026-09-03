using System.Net.Http.Headers;
using System.Text.Json;
using Cleanuparr.Domain.Entities.Deluge.Request;
using Cleanuparr.Domain.Entities.Deluge.Response;
using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Infrastructure.Features.DownloadClient.Deluge.Extensions;
using Cleanuparr.Infrastructure.Json;
using Cleanuparr.Persistence.Models.Configuration;

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
        "is_finished",
        "label",
        "seeding_time",
        "ratio",
        "total_seeds",
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
        return await SendRequest<bool>("auth.login", _config.Password ?? "");
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

    /// <summary>
    /// Gives the id of the single daemon that the Deluge Web UI knows.
    /// </summary>
    /// <remarks>
    /// Deluge gives one row of values for each host: the id, the host name, the
    /// port and the user name. The port is a number. A host row is not a list of
    /// strings. This method uses only the id.
    /// </remarks>
    public async Task<string?> GetHost()
    {
        List<List<JsonElement>?>? hosts = await SendRequest<List<List<JsonElement>?>?>("web.get_hosts");

        if (hosts?.Count > 1)
        {
            throw new FatalException("multiple Deluge hosts found - please connect to only one host");
        }

        JsonElement? id = hosts?.FirstOrDefault()?.FirstOrDefault();

        return id is { ValueKind: JsonValueKind.String } hostId ? hostId.GetString() : null;
    }

    public async Task<List<DelugeTorrent>> ListTorrents(Dictionary<string, string>? filters = null)
    {
        filters ??= new Dictionary<string, string>();
        var keys = typeof(DelugeTorrent).GetAllJsonPropertyFromType();
        Dictionary<string, DelugeTorrent>? result =
            await SendRequest<Dictionary<string, DelugeTorrent>>("core.get_torrents_status", filters, keys);
        return result?.Values.ToList() ?? [];
    }

    public async Task<List<DelugeTorrentExtended>> ListTorrentsExtended(Dictionary<string, string>? filters = null)
    {
        filters ??= new Dictionary<string, string>();
        var keys = typeof(DelugeTorrentExtended).GetAllJsonPropertyFromType();
        Dictionary<string, DelugeTorrentExtended>? result =
            await SendRequest<Dictionary<string, DelugeTorrentExtended>>("core.get_torrents_status", filters, keys);
        return result?.Values.ToList() ?? [];
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
        catch (DelugeClientException e) when (IsTorrentNotFoundError(e.Message, hash))
        {
            return null;
        }
    }

    private static bool IsTorrentNotFoundError(string? message, string hash)
    {
        if (string.IsNullOrEmpty(message))
        {
            return false;
        }

        return message.Contains("'NoneType' object has no attribute 'call'", StringComparison.Ordinal)
               || message.Contains("not in session", StringComparison.OrdinalIgnoreCase)
               || message.Contains($"KeyError: '{hash}'", StringComparison.OrdinalIgnoreCase);
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

        await SendRequest("core.set_torrent_options", hash, filePriorities);
    }

    /// <summary>
    /// Removes the torrents from Deluge.
    /// </summary>
    /// <remarks>
    /// Deluge answers with one row for each hash that it did not remove. A row holds
    /// the hash and the reason. A hash that is not in the session is already gone.
    /// Each other row keeps the torrent in Deluge, and it is an error.
    /// </remarks>
    public async Task DeleteTorrents(List<string> hashes, bool removeData)
    {
        List<List<JsonElement>?>? failures =
            await SendRequest<List<List<JsonElement>?>?>("core.remove_torrents", hashes, removeData);

        ThrowOnFailures(failures, "remove");
    }

    /// <summary>
    /// Pauses the torrents in Deluge.
    /// </summary>
    /// <remarks>
    /// Answers like <see cref="DeleteTorrents"/> does.
    /// </remarks>
    public async Task PauseTorrents(List<string> hashes)
    {
        List<List<JsonElement>?>? failures =
            await SendRequest<List<List<JsonElement>?>?>("core.pause_torrents", hashes);

        ThrowOnFailures(failures, "pause");
    }

    private static void ThrowOnFailures(List<List<JsonElement>?>? failures, string action)
    {
        List<string> errors = [];

        foreach (List<JsonElement>? failure in failures ?? [])
        {
            string hash = failure?.Count > 0 ? AsText(failure[0]) : string.Empty;
            string message = failure?.Count > 1 ? AsText(failure[1]) : string.Empty;

            if (IsTorrentNotFoundError(message, hash))
            {
                continue;
            }

            errors.Add($"{hash}: {message}");
        }

        if (errors.Count > 0)
        {
            throw new DelugeClientException(
                $"Deluge did not {action} {errors.Count} torrent(s) | {string.Join(" | ", errors)}");
        }
    }

    private static string AsText(JsonElement element) =>
        element.ValueKind is JsonValueKind.String ? element.GetString() ?? string.Empty : element.ToString();

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
    
    /// <summary>
    /// Sends a request and ignores the result.
    /// </summary>
    public async Task SendRequest(string method, params object[] parameters)
    {
        await SendRequest<JsonElement?>(CreateRequest(method, parameters));
    }

    public async Task<T?> SendRequest<T>(string method, params object[] parameters)
    {
        return await SendRequest<T>(CreateRequest(method, parameters));
    }

    /// <summary>
    /// Sends a request and reads the result as <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>
    /// Deluge answers each request with an envelope. The envelope holds an id, a
    /// result and an error. This method reads the envelope first, then it reads
    /// the result. An error from Deluge keeps its message. A result that does not
    /// agree with <typeparamref name="T"/> does not hide that message.
    /// </remarks>
    public async Task<T?> SendRequest<T>(DelugeRequest webRequest)
    {
        string requestJson = JsonSerializer.Serialize(webRequest, CleanuparrJsonOptions.Outbound);

        string responseJson = await PostJson(requestJson);

        DelugeResponse<JsonElement>? webResponse;

        try
        {
            webResponse = JsonSerializer.Deserialize<DelugeResponse<JsonElement>>(responseJson, CleanuparrJsonOptions.ExternalApiRead);
        }
        catch (JsonException exception)
        {
            throw new DelugeClientException(
                $"failed to deserialize Deluge response for method '{webRequest.Method}': {Truncate(responseJson)}",
                exception);
        }

        if (webResponse?.Error != null)
        {
            throw new DelugeClientException(webResponse.Error.Message);
        }

        if (webResponse?.ResponseId != webRequest.RequestId)
        {
            throw new DelugeClientException("desync");
        }

        if (webResponse.Result is not { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) } result)
        {
            return default;
        }

        try
        {
            return result.Deserialize<T>(CleanuparrJsonOptions.ExternalApiRead);
        }
        catch (JsonException exception)
        {
            throw new DelugeClientException(
                $"failed to deserialize the result of Deluge method '{webRequest.Method}': {Truncate(responseJson)}",
                exception);
        }
    }

    private static string Truncate(string responseJson) =>
        responseJson.Length > 2000 ? responseJson[..2000] : responseJson;

    public async Task<IReadOnlyList<string>> GetLabels()
    {
        return await SendRequest<IReadOnlyList<string>>("label.get_labels") ?? [];
    }

    public async Task CreateLabel(string label)
    {
        await SendRequest("label.add", label);
    }

    public async Task SetTorrentLabel(string hash, string newLabel)
    {
        await SendRequest("label.set_torrent", hash, newLabel);
    }
}
