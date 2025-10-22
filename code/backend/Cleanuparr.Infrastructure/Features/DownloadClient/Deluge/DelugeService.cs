using Cleanuparr.Domain.Entities.Deluge.Response;
using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Infrastructure.Events;
using Cleanuparr.Infrastructure.Features.Files;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Features.MalwareBlocker;
using Cleanuparr.Infrastructure.Http;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence.Models.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Deluge;

public partial class DelugeService : DownloadService, IDelugeService
{
    private readonly DelugeClient _client;

    public DelugeService(
        ILogger<DelugeService> logger,
        IMemoryCache cache,
        IFilenameEvaluator filenameEvaluator,
        IStriker striker,
        IDryRunInterceptor dryRunInterceptor,
        IHardLinkFileService hardLinkFileService,
        IDynamicHttpClientProvider httpClientProvider,
        EventPublisher eventPublisher,
        BlocklistProvider blocklistProvider,
        DownloadClientConfig downloadClientConfig,
        IRuleEvaluator ruleEvaluator,
        IRuleManager ruleManager
    ) : base(
        logger, cache,
        filenameEvaluator, striker, dryRunInterceptor, hardLinkFileService,
        httpClientProvider, eventPublisher, blocklistProvider, downloadClientConfig, ruleEvaluator, ruleManager
    )
    {
        _client = new DelugeClient(downloadClientConfig, _httpClient);
    }
    
    public override async Task LoginAsync()
    {
        try 
        {
            await _client.LoginAsync();
            
            if (!await _client.IsConnected() && !await _client.Connect())
            {
                throw new FatalException("Deluge WebUI is not connected to the daemon");
            }
            
            _logger.LogDebug("Successfully logged in to Deluge client {clientId}", _downloadClientConfig.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to login to Deluge client {clientId}", _downloadClientConfig.Id);
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
                // If credentials are provided, we must be able to login and connect for the service to be healthy
                await _client.LoginAsync();
                
                if (!await _client.IsConnected() && !await _client.Connect())
                {
                    throw new Exception("Deluge WebUI is not connected to the daemon");
                }
                
                _logger.LogDebug("Health check: Successfully logged in to Deluge client {clientId}", _downloadClientConfig.Id);
            }
            else
            {
                // If no credentials, test basic connectivity to the web UI
                // We'll try a simple HTTP request to verify the service is running
                if (_httpClient == null)
                {
                    throw new InvalidOperationException("HTTP client is not initialized");
                }
                
                var response = await _httpClient.GetAsync("/");
                if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.Unauthorized)
                {
                    throw new Exception($"Service returned status code: {response.StatusCode}");
                }
                
                _logger.LogDebug("Health check: Successfully connected to Deluge client {clientId}", _downloadClientConfig.Id);
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
            
            _logger.LogWarning(ex, "Health check failed for Deluge client {clientId}", _downloadClientConfig.Id);
            
            return new HealthCheckResult
            {
                IsHealthy = false,
                ErrorMessage = $"Connection failed: {ex.Message}",
                ResponseTime = stopwatch.Elapsed
            };
        }
    }

    private static void ProcessFiles(Dictionary<string, DelugeFileOrDirectory>? contents, Action<string, DelugeFileOrDirectory> processFile)
    {
        if (contents is null)
        {
            return;
        }
        
        foreach (var (name, data) in contents)
        {
            switch (data.Type)
            {
                case "file":
                    processFile(name, data);
                    break;
                case "dir" when data.Contents is not null:
                    // Recurse into subdirectories
                    ProcessFiles(data.Contents, processFile);
                    break;
            }
        }
    }

    public override void Dispose()
    {
    }
}