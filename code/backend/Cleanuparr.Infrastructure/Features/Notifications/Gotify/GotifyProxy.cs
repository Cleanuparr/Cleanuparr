using System.Text;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Cleanuparr.Shared.Helpers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Cleanuparr.Infrastructure.Features.Notifications.Gotify;

public sealed class GotifyProxy : IGotifyProxy
{
    private readonly ILogger<GotifyProxy> _logger;
    private readonly HttpClient _httpClient;

    public GotifyProxy(ILogger<GotifyProxy> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient(Constants.HttpClientWithRetryName);
    }

    public async Task SendNotification(GotifyPayload payload, GotifyConfig config)
    {
        try
        {
            string baseUrl = config.ServerUrl.TrimEnd('/');
            string url = $"{baseUrl}/message?token={config.ApplicationToken}";

            string content = JsonConvert.SerializeObject(payload, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore
            });

            _logger.LogTrace("sending notification to Gotify: {content}", content);

            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(content, Encoding.UTF8, "application/json");

            using HttpResponseMessage response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException exception)
        {
            if (exception.StatusCode is null)
            {
                throw new GotifyException("unable to send notification", exception);
            }

            switch ((int)exception.StatusCode)
            {
                case 401:
                case 403:
                    throw new GotifyException("unable to send notification | application token is invalid or unauthorized");
                case 404:
                    throw new GotifyException("unable to send notification | Gotify server not found");
                case 502:
                case 503:
                case 504:
                    throw new GotifyException("unable to send notification | Gotify service unavailable", exception);
                default:
                    throw new GotifyException("unable to send notification", exception);
            }
        }
    }
}
