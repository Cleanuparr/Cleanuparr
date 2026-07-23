using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence.Models.Configuration.General;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Services;

public sealed class ConnectivityChecker : IConnectivityChecker
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(10);

    private readonly ILogger<ConnectivityChecker> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public ConnectivityChecker(ILogger<ConnectivityChecker> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<bool> IsOnlineAsync(GeneralConfig config, CancellationToken cancellationToken = default)
    {
        if (!config.ConnectivityCheckEnabled || config.ConnectivityCheckUrls.Count is 0)
        {
            return true;
        }

        using HttpClient client = _httpClientFactory.CreateClient();

        foreach (string url in config.ConnectivityCheckUrls)
        {
            try
            {
                using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(ProbeTimeout);

                using HttpResponseMessage response = await client
                    .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }

                _logger.LogDebug("connectivity probe returned {status} | {url}", (int)response.StatusCode, url);
            }
            catch (Exception exception)
            {
                _logger.LogDebug(exception, "connectivity probe failed | {url}", url);
            }
        }

        return false;
    }
}
