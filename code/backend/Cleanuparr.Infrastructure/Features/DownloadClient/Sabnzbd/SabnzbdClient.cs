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
    private SabnzbdMiscConfig? _cachedMiscConfig;
    private bool _miscConfigFetched;

    public SabnzbdClient(DownloadClientConfig config, HttpClient httpClient)
    {
        _config = config;
        _httpClient = httpClient;
    }

    /// <summary>
    /// Confirms the API key is accepted. <c>mode=version</c> answers without one, so login/health checks use
    /// <c>mode=queue</c> instead, which SABnzbd rejects with HTTP 403 for a missing or wrong key.
    /// </summary>
    public async Task ValidateApiKeyAsync() =>
        await SendRequestAsync<SabnzbdQueueResponse>("queue", ("limit", "1"));

    /// <summary>
    /// Fetches the queue, optionally filtered to a single job.
    /// </summary>
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

    /// <summary>
    /// Fetches the full history (not SABnzbd's default short window), optionally filtered to a single job.
    /// </summary>
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

    /// <summary>
    /// The incomplete-downloads folder SABnzbd is configured with (<c>misc.download_dir</c>).
    /// </summary>
    public async Task<string?> GetDownloadDirAsync() => (await GetMiscConfigAsync())?.DownloadDir;

    private async Task<SabnzbdMiscConfig?> GetMiscConfigAsync()
    {
        if (_miscConfigFetched)
        {
            return _cachedMiscConfig;
        }

        SabnzbdGetConfigResponse? response = await SendRequestAsync<SabnzbdGetConfigResponse>("get_config", ("section", "misc"));
        _cachedMiscConfig = response?.Config?.Misc;
        _miscConfigFetched = true;
        return _cachedMiscConfig;
    }

    /// <summary>
    /// Deletes a job from the queue, optionally deleting its on-disk files too.
    /// </summary>
    public async Task DeleteFromQueueAsync(string nzoId, bool deleteFiles) =>
        await SendActionAsync("queue", ("name", "delete"), ("value", nzoId), ("del_files", deleteFiles ? "1" : "0"));

    /// <summary>
    /// Deletes a job from history for good (forces <c>archive=0</c>). SABnzbd's own <c>del_files</c> only
    /// removes files for a <b>Failed</b> job; it is ignored for a Completed job.
    /// </summary>
    public async Task DeleteFromHistoryAsync(string nzoId, bool deleteFiles) =>
        await SendActionAsync("history", ("name", "delete"), ("value", nzoId), ("del_files", deleteFiles ? "1" : "0"), ("archive", "0"));

    private async Task SendActionAsync(string mode, params (string Key, string Value)[] parameters) =>
        await SendRequestAsync<SabnzbdActionResponse>(mode, parameters);

    private async Task<T?> SendRequestAsync<T>(string mode, params (string Key, string Value)[] parameters)
    {
        string url = BuildUrl(mode, parameters);
        HttpResponseMessage response;

        try
        {
            response = await _httpClient.GetAsync(url);
        }
        catch (HttpRequestException exception)
        {
            throw new SabnzbdClientException($"SABnzbd request failed for mode '{mode}': {exception.Message}", exception);
        }
        catch (TaskCanceledException exception)
        {
            // no CancellationToken is passed in, so this is always the HttpClient timeout firing.
            throw new SabnzbdClientException($"SABnzbd request failed for mode '{mode}': {exception.Message}", exception);
        }

        using (response)
        {
            string responseBody = await response.Content.ReadAsStringAsync();

            // A bad or missing apikey comes back as plain text ("API Key Incorrect") with HTTP 403, not the
            // JSON envelope checked below, so it is surfaced here from the status code.
            if (!response.IsSuccessStatusCode)
            {
                throw new SabnzbdClientException($"SABnzbd request failed for mode '{mode}': {response.StatusCode} {Truncate(responseBody)}");
            }

            try
            {
                // SABnzbd reports errors as HTTP 200 with {"status":false,"error":"..."}, on any mode.
                SabnzbdActionResponse? envelope = JsonSerializer.Deserialize<SabnzbdActionResponse>(responseBody, CleanuparrJsonOptions.ExternalApiRead);

                if (envelope is { Status: false })
                {
                    throw new SabnzbdClientException($"SABnzbd request failed for mode '{mode}': {envelope.Error}");
                }

                return JsonSerializer.Deserialize<T>(responseBody, CleanuparrJsonOptions.ExternalApiRead);
            }
            catch (JsonException exception)
            {
                throw new SabnzbdClientException($"failed to deserialize SABnzbd response for mode '{mode}': {Truncate(responseBody)}", exception);
            }
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
