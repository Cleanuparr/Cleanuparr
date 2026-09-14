using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.Auth;

/// <summary>
/// Background service that periodically drops expired OIDC flow states and one-time codes.
/// One-time codes hold real access and refresh tokens, so they are purged rather than left
/// to be evicted by the next login.
/// </summary>
public sealed class OidcCleanupService : BackgroundService
{
    private readonly ILogger<OidcCleanupService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _sweepInterval = TimeSpan.FromMinutes(1);

    public OidcCleanupService(ILogger<OidcCleanupService> logger, TimeProvider timeProvider)
    {
        _logger = logger;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_sweepInterval, _timeProvider, stoppingToken);

                OidcAuthService.CleanupExpiredEntries(_timeProvider);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during OIDC cleanup");
            }
        }
    }
}
