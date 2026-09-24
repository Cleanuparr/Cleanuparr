using Cleanuparr.Persistence.Models.Configuration.Notification;
using Cleanuparr.Shared.Helpers;
using Microsoft.Extensions.Logging;
using Cleanuparr.Infrastructure.Json;

namespace Cleanuparr.Infrastructure.Features.Notifications.Gotify;

public sealed class GotifyProxy : IGotifyProxy
{
    private readonly ILogger<GotifyProxy> _logger;
    private readonly HttpClient _httpClient;

    private static readonly IReadOnlyDictionary<int, (string Message, bool IncludeException)> StatusCodeMessages =
        new Dictionary<int, (string, bool)>
        {
            [401] = ("unable to send notification | application token is invalid or unauthorized", false),
            [403] = ("unable to send notification | application token is invalid or unauthorized", false),
            [404] = ("unable to send notification | Gotify server not found", false),
            [502] = ("unable to send notification | Gotify service unavailable", true),
            [503] = ("unable to send notification | Gotify service unavailable", true),
            [504] = ("unable to send notification | Gotify service unavailable", true),
        };

    public GotifyProxy(ILogger<GotifyProxy> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient(Constants.HttpClientWithRetryName);
    }

    public async Task SendNotification(GotifyPayload payload, GotifyConfig config)
    {
        string baseUrl = config.ServerUrl.TrimEnd('/');
        string url = $"{baseUrl}/message?token={config.ApplicationToken}";

        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);

        await NotificationHttpSender.SendAsync(
            _httpClient,
            request,
            payload,
            CleanuparrJsonOptions.Notification,
            content => _logger.LogTrace("sending notification to Gotify: {content}", content),
            (message, exception) => exception is null ? new GotifyException(message) : new GotifyException(message, exception),
            StatusCodeMessages);
    }
}
