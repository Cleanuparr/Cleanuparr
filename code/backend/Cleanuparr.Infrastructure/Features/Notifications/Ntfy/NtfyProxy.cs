using System.Net.Http.Headers;
using System.Text;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Cleanuparr.Shared.Helpers;
using Cleanuparr.Infrastructure.Json;

namespace Cleanuparr.Infrastructure.Features.Notifications.Ntfy;

public sealed class NtfyProxy : INtfyProxy
{
    private readonly HttpClient _httpClient;

    private static readonly IReadOnlyDictionary<int, (string Message, bool IncludeException)> StatusCodeMessages =
        new Dictionary<int, (string, bool)>
        {
            [400] = ("Bad request - invalid topic or payload", true),
            [401] = ("Unauthorized - invalid credentials", true),
            [413] = ("Payload too large", true),
            [429] = ("Rate limited - too many requests", true),
            [507] = ("Insufficient storage on server", true),
        };

    public NtfyProxy(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient(Constants.HttpClientWithRetryName);
    }

    public async Task SendNotification(NtfyPayload payload, NtfyConfig config)
    {
        var parsedUrl = config.Uri!;
        using HttpRequestMessage request = new(HttpMethod.Post, parsedUrl);

        // Set authentication headers based on configuration
        SetAuthenticationHeaders(request, config);

        await NotificationHttpSender.SendAsync(
            _httpClient,
            request,
            payload,
            CleanuparrJsonOptions.Notification,
            logTrace: null,
            (message, exception) => exception is null ? new NtfyException(message) : new NtfyException(message, exception),
            StatusCodeMessages,
            defaultErrorMessage: "Unable to send notification");
    }

    private static void SetAuthenticationHeaders(HttpRequestMessage request, NtfyConfig config)
    {
        switch (config.AuthenticationType)
        {
            case NtfyAuthenticationType.BasicAuth:
                if (!string.IsNullOrWhiteSpace(config.Username) && !string.IsNullOrWhiteSpace(config.Password))
                {
                    var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{config.Username}:{config.Password}"));
                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                }
                break;

            case NtfyAuthenticationType.AccessToken:
                if (!string.IsNullOrWhiteSpace(config.AccessToken))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.AccessToken);
                }
                break;

            case NtfyAuthenticationType.None:
            default:
                // No authentication required
                break;
        }
    }
}
