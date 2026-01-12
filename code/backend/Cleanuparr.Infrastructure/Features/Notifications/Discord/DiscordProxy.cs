using System.Text;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Cleanuparr.Shared.Helpers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Cleanuparr.Infrastructure.Features.Notifications.Discord;

public sealed class DiscordProxy : IDiscordProxy
{
    private readonly ILogger<DiscordProxy> _logger;
    private readonly HttpClient _httpClient;

    public DiscordProxy(ILogger<DiscordProxy> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient(Constants.HttpClientWithRetryName);
    }

    public async Task SendNotification(DiscordPayload payload, DiscordConfig config)
    {
        try
        {
            string content = JsonConvert.SerializeObject(payload, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore
            });

            _logger.LogTrace("sending notification to Discord: {content}", content);

            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, config.WebhookUrl);
            request.Content = new StringContent(content, Encoding.UTF8, "application/json");

            using HttpResponseMessage response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException exception)
        {
            if (exception.StatusCode is null)
            {
                throw new DiscordException("unable to send notification", exception);
            }

            switch ((int)exception.StatusCode)
            {
                case 401:
                case 403:
                    throw new DiscordException("unable to send notification | webhook URL is invalid or unauthorized");
                case 404:
                    throw new DiscordException("unable to send notification | webhook not found");
                case 429:
                    throw new DiscordException("unable to send notification | rate limited, please try again later", exception);
                case 502:
                case 503:
                case 504:
                    throw new DiscordException("unable to send notification | Discord service unavailable", exception);
                default:
                    throw new DiscordException("unable to send notification", exception);
            }
        }
    }
}
