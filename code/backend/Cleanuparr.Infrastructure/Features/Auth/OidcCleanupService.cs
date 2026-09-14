using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.Auth;

/// <summary>
/// Drops expired OIDC flow states and one-time codes on a timer.
/// One-time codes hold live tokens, so logins must not be the only thing clearing them.
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
