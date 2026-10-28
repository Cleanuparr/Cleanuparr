using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Json;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Cleanuparr.Shared.Helpers;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.Notifications.Webhook;

public sealed class WebhookProxy : IWebhookProxy
{
    private readonly ILogger<WebhookProxy> _logger;
    private readonly HttpClient _httpClient;

    private static readonly IReadOnlyDictionary<int, (string Message, bool IncludeException)> StatusCodeMessages =
        new Dictionary<int, (string, bool)>
        {
            [401] = ("Unable to send notification | webhook rejected the credentials", false),
            [403] = ("Unable to send notification | webhook rejected the credentials", false),
            [404] = ("Unable to send notification | webhook URL not found", false),
            [502] = ("Unable to send notification | webhook service unavailable", true),
            [503] = ("Unable to send notification | webhook service unavailable", true),
            [504] = ("Unable to send notification | webhook service unavailable", true),
        };

    public WebhookProxy(ILogger<WebhookProxy> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient(Constants.HttpClientWithRetryName);
    }

    public async Task SendNotification(WebhookPayload payload, WebhookConfig config)
    {
        HttpMethod method = config.Method == WebhookMethod.Put ? HttpMethod.Put : HttpMethod.Post;

        using HttpRequestMessage request = new HttpRequestMessage(method, config.Url);

        foreach (string header in config.Headers)
        {
            if (WebhookConfig.TrySplitHeader(header, out string name, out string value)
                && !request.Headers.TryAddWithoutValidation(name, value))
            {
                throw new WebhookException($"Webhook header '{name}' cannot be sent as a request header");
            }
        }

        await NotificationHttpSender.SendAsync(
            _httpClient,
            request,
            payload,
            CleanuparrJsonOptions.NotificationIncludeNulls,
            content => _logger.LogTrace("sending notification to Webhook: {Content}", content),
            (message, exception) => exception is null ? new WebhookException(message) : new WebhookException(message, exception),
            StatusCodeMessages);
    }
}
