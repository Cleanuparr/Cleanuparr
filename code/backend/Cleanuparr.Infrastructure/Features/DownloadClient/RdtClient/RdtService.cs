using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Cleanuparr.Infrastructure.Events;
using Cleanuparr.Infrastructure.Features.Files;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Features.MalwareBlocker;
using Cleanuparr.Infrastructure.Http;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Persistence.Models.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.RdtClient;

public partial class RdtService : DownloadService, IRdtService
{
    private readonly string _apiBaseUrl;

    public RdtService(
        ILogger<RdtService> logger,
        IMemoryCache cache,
        IFilenameEvaluator filenameEvaluator,
        IStriker striker,
        IDryRunInterceptor dryRunInterceptor,
        IHardLinkFileService hardLinkFileService,
        IDynamicHttpClientProvider httpClientProvider,
        EventPublisher eventPublisher,
        BlocklistProvider blocklistProvider,
        DownloadClientConfig downloadClientConfig
    ) : base(
        logger, cache, filenameEvaluator, striker, dryRunInterceptor, hardLinkFileService,
        httpClientProvider, eventPublisher, blocklistProvider, downloadClientConfig
    )
    {
        // RDT-Client implements qBittorrent API at /api/v2
        _apiBaseUrl = downloadClientConfig.Host!.ToString().TrimEnd('/') + "/api/v2";
    }
    
    public override async Task LoginAsync()
    {
        // RDT-Client supports two auth modes: user/password and no auth
        if (string.IsNullOrEmpty(_downloadClientConfig.Username) && string.IsNullOrEmpty(_downloadClientConfig.Password))
        {
            _logger.LogDebug("No credentials configured for RDT-Client {clientId}, skipping login", _downloadClientConfig.Id);
            return;
        }

        try
        {
            // Make direct HTTP POST request to /api/v2/auth/login with form-encoded data
            var loginUrl = $"{_apiBaseUrl}/auth/login";

            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("username", _downloadClientConfig.Username ?? ""),
                new KeyValuePair<string, string>("password", _downloadClientConfig.Password ?? "")
            });

            var response = await _httpClient.PostAsync(loginUrl, formData);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Login failed with status {response.StatusCode}: {responseContent}");
            }

            if (responseContent.Contains("Fails"))
            {
                throw new UnauthorizedAccessException("Login failed: Invalid credentials");
            }

            _logger.LogDebug("Successfully logged in to RDT-Client {clientId}", _downloadClientConfig.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to login to RDT-Client {clientId}", _downloadClientConfig.Id);
            throw;
        }
    }

    public override async Task<HealthCheckResult> HealthCheckAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            bool hasCredentials = !string.IsNullOrEmpty(_downloadClientConfig.Username) ||
                                  !string.IsNullOrEmpty(_downloadClientConfig.Password);

            if (hasCredentials)
            {
                // Test by logging in
                await LoginAsync();
            }
            else
            {
                // Test connectivity using version endpoint
                var versionUrl = $"{_apiBaseUrl}/app/version";
                var response = await _httpClient.GetAsync(versionUrl);
                response.EnsureSuccessStatusCode();
            }

            stopwatch.Stop();

            return new HealthCheckResult
            {
                IsHealthy = true,
                ResponseTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogWarning(ex, "Health check failed for RDT-Client {clientId}", _downloadClientConfig.Id);

            return new HealthCheckResult
            {
                IsHealthy = false,
                ErrorMessage = $"Connection failed: {ex.Message}",
                ResponseTime = stopwatch.Elapsed
            };
        }
    }


    protected async Task<List<RdtTorrentInfo>> GetTorrentListAsync(string? filter = null, string? category = null)
    {
        var url = $"{_apiBaseUrl}/torrents/info";
        var queryParams = new List<string>();

        if (!string.IsNullOrEmpty(filter))
            queryParams.Add($"filter={filter}");
        if (!string.IsNullOrEmpty(category))
            queryParams.Add($"category={Uri.EscapeDataString(category)}");

        if (queryParams.Count > 0)
            url += "?" + string.Join("&", queryParams);

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<RdtTorrentInfo>>() ?? new List<RdtTorrentInfo>();
    }

    protected async Task<List<RdtTorrentContent>> GetTorrentContentsAsync(string hash)
    {
        var url = $"{_apiBaseUrl}/torrents/files?hash={hash}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<RdtTorrentContent>>() ?? new List<RdtTorrentContent>();
    }

    protected async Task<Dictionary<string, RdtCategory>> GetCategoriesAsync()
    {
        var url = $"{_apiBaseUrl}/torrents/categories";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Dictionary<string, RdtCategory>>() ?? new Dictionary<string, RdtCategory>();
    }

    protected async Task AddCategoryAsync(string name)
    {
        var url = $"{_apiBaseUrl}/torrents/createCategory";
        var formData = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("category", name)
        });

        var response = await _httpClient.PostAsync(url, formData);
        response.EnsureSuccessStatusCode();
    }

    protected async Task SetTorrentCategoryAsync(string[] hashes, string category)
    {
        var url = $"{_apiBaseUrl}/torrents/setCategory";
        var formData = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("hashes", string.Join("|", hashes)),
            new KeyValuePair<string, string>("category", category)
        });

        var response = await _httpClient.PostAsync(url, formData);
        response.EnsureSuccessStatusCode();
    }


    protected async Task DeleteAsync(string[] hashes, bool deleteFiles)
    {
        var url = $"{_apiBaseUrl}/torrents/delete";
        var formData = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("hashes", string.Join("|", hashes)),
            new KeyValuePair<string, string>("deleteFiles", deleteFiles.ToString().ToLower())
        });

        var response = await _httpClient.PostAsync(url, formData);
        response.EnsureSuccessStatusCode();
    }

    public override void Dispose()
    {
        // No resources to dispose
    }

    // DTOs matching RDT-Client API responses
    public class RdtTorrentInfo
    {
        public string? Hash { get; set; }
        public string? Name { get; set; }
        public string? Category { get; set; }
        public string? Tags { get; set; }
        public double Ratio { get; set; }
        public TimeSpan? SeedingTime { get; set; }
        public string? SavePath { get; set; }
    }

    public class RdtTorrentContent
    {
        public int? Index { get; set; }
        public string? Name { get; set; }
        public int Priority { get; set; }
    }

    public class RdtCategory
    {
        public string? Name { get; set; }
        public string? SavePath { get; set; }
    }
}