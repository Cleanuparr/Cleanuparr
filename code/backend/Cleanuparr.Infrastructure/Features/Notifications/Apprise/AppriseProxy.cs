using System.Net.Http.Headers;
using System.Text;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Cleanuparr.Shared.Helpers;
using Cleanuparr.Infrastructure.Json;

namespace Cleanuparr.Infrastructure.Features.Notifications.Apprise;

public sealed class AppriseProxy : IAppriseProxy
{
    private readonly HttpClient _httpClient;

    private static readonly IReadOnlyDictionary<int, (string Message, bool IncludeException)> StatusCodeMessages =
        new Dictionary<int, (string, bool)>
        {
            [401] = ("Unable to send notification | API key is invalid", false),
            [424] = ("Your tags are not configured correctly", true),
            [502] = ("Unable to send notification | service unavailable", true),
            [503] = ("Unable to send notification | service unavailable", true),
            [504] = ("Unable to send notification | service unavailable", true),
        };

    public AppriseProxy(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient(Constants.HttpClientWithRetryName);
    }

    public async Task SendNotification(ApprisePayload payload, AppriseConfig config)
    {
        var parsedUrl = config.Uri!;
        UriBuilder uriBuilder = new(parsedUrl);
        uriBuilder.Path = $"{uriBuilder.Path.TrimEnd('/')}/notify/{config.Key}";

        using HttpRequestMessage request = new(HttpMethod.Post, uriBuilder.Uri);

        if (!string.IsNullOrEmpty(parsedUrl.UserInfo))
        {
            var byteArray = Encoding.ASCII.GetBytes(parsedUrl.UserInfo);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
        }

        await NotificationHttpSender.SendAsync(
            _httpClient,
            request,
            payload,
            CleanuparrJsonOptions.Notification,
            logTrace: null,
            (message, exception) => exception is null ? new AppriseException(message) : new AppriseException(message, exception),
            StatusCodeMessages,
            defaultErrorMessage: "Unable to send notification");
    }
}
