using Cleanuparr.Persistence.Models.Configuration.Notification;
using Cleanuparr.Shared.Helpers;
using Cleanuparr.Infrastructure.Json;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.Notifications.Notifiarr;

public sealed class NotifiarrProxy : INotifiarrProxy
{
    private readonly ILogger<NotifiarrProxy> _logger;
    private readonly HttpClient _httpClient;

    private const string Url = "https://notifiarr.com/api/v1/notification/passthrough/";

    private static readonly IReadOnlyDictionary<int, (string Message, bool IncludeException)> StatusCodeMessages =
        new Dictionary<int, (string, bool)>
        {
            [401] = ("Unable to send notification | API key is invalid", false),
            [502] = ("Unable to send notification | service unavailable", true),
            [503] = ("Unable to send notification | service unavailable", true),
            [504] = ("Unable to send notification | service unavailable", true),
        };

    public NotifiarrProxy(ILogger<NotifiarrProxy> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient(Constants.HttpClientWithRetryName);
    }

    public async Task SendNotification(NotifiarrPayload payload, NotifiarrConfig config)
    {
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"{Url}{config.ApiKey}");

        await NotificationHttpSender.SendAsync(
            _httpClient,
            request,
            payload,
            CleanuparrJsonOptions.NotificationIncludeNulls,
            content => _logger.LogTrace("sending notification to Notifiarr: {Content}", content),
            (message, exception) => exception is null ? new NotifiarrException(message) : new NotifiarrException(message, exception),
            StatusCodeMessages);
    }
}
