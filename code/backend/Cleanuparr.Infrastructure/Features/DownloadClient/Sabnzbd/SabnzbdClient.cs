using System.Text;
using System.Text.Json;
using Cleanuparr.Domain.Entities.Sabnzbd;
using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Infrastructure.Json;
using Cleanuparr.Persistence.Models.Configuration;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Sabnzbd;

/// <summary>
/// Raw HTTP client for the SABnzbd API (<c>{host}/api?mode=...&amp;output=json&amp;apikey=...</c>).
/// </summary>
public sealed class SabnzbdClient
{
    private readonly DownloadClientConfig _config;
    private readonly HttpClient _httpClient;

    public SabnzbdClient(DownloadClientConfig config, HttpClient httpClient)
    {
        _config = config;
        _httpClient = httpClient;
    }

    public async Task<string?> GetVersionAsync()
    {
        SabnzbdVersionResponse? response = await SendRequestAsync<SabnzbdVersionResponse>("version");
        return response?.Version;
    }

    public async Task<SabnzbdQueueData?> GetQueueAsync(string? nzoId = null)
    {
        List<(string Key, string Value)> parameters = [];

        if (!string.IsNullOrEmpty(nzoId))
        {
            parameters.Add(("nzo_ids", nzoId));
        }

        SabnzbdQueueResponse? response = await SendRequestAsync<SabnzbdQueueResponse>("queue", parameters.ToArray());
        return response?.Queue;
    }

    public async Task<SabnzbdHistoryData?> GetHistoryAsync(string? nzoId = null)
    {
        // limit=0 asks SABnzbd for the full history instead of its default short window.
        List<(string Key, string Value)> parameters = [("limit", "0")];

        if (!string.IsNullOrEmpty(nzoId))
        {
            parameters.Add(("nzo_ids", nzoId));
        }

        SabnzbdHistoryResponse? response = await SendRequestAsync<SabnzbdHistoryResponse>("history", parameters.ToArray());
        return response?.History;
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync()
    {
        SabnzbdCategoriesResponse? response = await SendRequestAsync<SabnzbdCategoriesResponse>("get_cats");
        return response?.Categories ?? [];
    }

    public async Task DeleteFromQueueAsync(string nzoId, bool deleteFiles) =>
        await SendActionAsync("queue", ("name", "delete"), ("value", nzoId), ("del_files", deleteFiles ? "1" : "0"));

    public async Task DeleteFromHistoryAsync(string nzoId, bool deleteFiles) =>
        await SendActionAsync("history", ("name", "delete"), ("value", nzoId), ("del_files", deleteFiles ? "1" : "0"));

    public async Task PauseAsync(string nzoId) =>
        await SendActionAsync("queue", ("name", "pause"), ("value", nzoId));

    public async Task ChangeCategoryAsync(string nzoId, string category) =>
        await SendActionAsync("change_cat", ("value", nzoId), ("value2", category));

    private async Task SendActionAsync(string mode, params (string Key, string Value)[] parameters)
    {
        SabnzbdActionResponse? response = await SendRequestAsync<SabnzbdActionResponse>(mode, parameters);

        if (response is { Status: false })
        {
            throw new SabnzbdClientException($"SABnzbd action '{mode}' failed: {response.Error}");
        }
    }

    private async Task<T?> SendRequestAsync<T>(string mode, params (string Key, string Value)[] parameters)
    {
        string url = BuildUrl(mode, parameters);
        string responseJson;

        try
        {
            responseJson = await _httpClient.GetStringAsync(url);
        }
        catch (HttpRequestException exception)
        {
            throw new SabnzbdClientException($"SABnzbd request failed for mode '{mode}': {exception.Message}", exception);
        }

        try
        {
            return JsonSerializer.Deserialize<T>(responseJson, CleanuparrJsonOptions.ExternalApiRead);
        }
        catch (JsonException exception)
        {
            throw new SabnzbdClientException($"failed to deserialize SABnzbd response for mode '{mode}': {Truncate(responseJson)}", exception);
        }
    }

    private string BuildUrl(string mode, params (string Key, string Value)[] parameters)
    {
        UriBuilder uriBuilder = new(_config.Url);
        uriBuilder.Path = $"{uriBuilder.Path.TrimEnd('/')}/api";

        StringBuilder query = new();
        query.Append("mode=").Append(Uri.EscapeDataString(mode));
        query.Append("&output=json");
        query.Append("&apikey=").Append(Uri.EscapeDataString(_config.ApiKey ?? string.Empty));

        foreach ((string key, string value) in parameters)
        {
            query.Append('&').Append(Uri.EscapeDataString(key)).Append('=').Append(Uri.EscapeDataString(value));
        }

        uriBuilder.Query = query.ToString();
        return uriBuilder.Uri.ToString();
    }

    private static string Truncate(string responseJson) =>
        responseJson.Length > 2000 ? responseJson[..2000] : responseJson;
}
