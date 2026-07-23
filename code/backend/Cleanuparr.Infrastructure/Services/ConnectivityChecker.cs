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
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(ProbeTimeout);

        bool online = false;
        ParallelOptions options = new()
        {
            MaxDegreeOfParallelism = 5,
            CancellationToken = cts.Token,
        };

        try
        {
            await Parallel.ForEachAsync(config.ConnectivityCheckUrls, options, async (url, token) =>
            {
                if (await ProbeAsync(client, url, token))
                {
                    online = true;
                    await cts.CancelAsync();
                }
            });
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }

        cancellationToken.ThrowIfCancellationRequested();
        return online;
    }

    private async Task<bool> ProbeAsync(HttpClient client, string url, CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await client
                .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            _logger.LogDebug("Connectivity probe returned {status} | {url}", (int)response.StatusCode, url);
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Connectivity probe failed | {url}", url);
            return false;
        }
    }
}
