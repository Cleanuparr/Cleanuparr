using Cleanuparr.Persistence;
using Cleanuparr.Shared.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DelugeService = Cleanuparr.Infrastructure.Features.DownloadClient.Deluge.DelugeService;

namespace Cleanuparr.Infrastructure.Http.DynamicHttpClientSystem;

/// <summary>
/// Background service to pre-register standard HttpClient configurations
/// </summary>
public class HttpClientConfigurationService : IHostedService
{
    private readonly IDynamicHttpClientFactory _clientFactory;
    private readonly ILogger<HttpClientConfigurationService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public HttpClientConfigurationService(
        IDynamicHttpClientFactory clientFactory, 
        ILogger<HttpClientConfigurationService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _clientFactory = clientFactory;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            await using var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();
            
            var config = await dataContext.GeneralConfigs
                .AsNoTracking()
                .FirstAsync(cancellationToken);
            
            // Register the retry client (equivalent to Constants.HttpClientWithRetryName)
            _clientFactory.RegisterRetryClient(
                Constants.HttpClientWithRetryName,
                config.HttpTimeout,
                new RetryConfig
                {
                    MaxRetries = config.HttpMaxRetries,
                    ExcludeUnauthorized = true
                },
                config.HttpCertificateValidation
            );

            // Register the Deluge client
            _clientFactory.RegisterDelugeClient(
                nameof(DelugeService),
                config.HttpTimeout,
                new RetryConfig
                {
                    MaxRetries = config.HttpMaxRetries,
                    ExcludeUnauthorized = true
                },
                config.HttpCertificateValidation
            );
            
            _logger.LogInformation("Pre-registered standard HTTP client configurations");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pre-register HTTP client configurations");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
} 