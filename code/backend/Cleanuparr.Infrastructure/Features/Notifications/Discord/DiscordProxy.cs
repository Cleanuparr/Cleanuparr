using Cleanuparr.Persistence.Models.Configuration.Notification;
using Cleanuparr.Shared.Helpers;
using Microsoft.Extensions.Logging;
using Cleanuparr.Infrastructure.Json;

namespace Cleanuparr.Infrastructure.Features.Notifications.Discord;

public sealed class DiscordProxy : IDiscordProxy
{
    private readonly ILogger<DiscordProxy> _logger;
    private readonly HttpClient _httpClient;

    private static readonly IReadOnlyDictionary<int, (string Message, bool IncludeException)> StatusCodeMessages =
        new Dictionary<int, (string, bool)>
        {
            [401] = ("Unable to send notification | webhook URL is invalid or unauthorized", false),
            [403] = ("Unable to send notification | webhook URL is invalid or unauthorized", false),
            [404] = ("Unable to send notification | webhook not found", false),
            [429] = ("Unable to send notification | rate limited, please try again later", true),
            [502] = ("Unable to send notification | Discord service unavailable", true),
            [503] = ("Unable to send notification | Discord service unavailable", true),
            [504] = ("Unable to send notification | Discord service unavailable", true),
        };

    public DiscordProxy(ILogger<DiscordProxy> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient(Constants.HttpClientWithRetryName);
    }

    public async Task SendNotification(DiscordPayload payload, DiscordConfig config)
    {
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, config.WebhookUrl);

        await NotificationHttpSender.SendAsync(
            _httpClient,
            request,
            payload,
            CleanuparrJsonOptions.Notification,
            content => _logger.LogTrace("sending notification to Discord: {Content}", content),
            (message, exception) => exception is null ? new DiscordException(message) : new DiscordException(message, exception),
            StatusCodeMessages);
    }
}
