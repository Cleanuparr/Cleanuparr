using System.Text;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Cleanuparr.Shared.Helpers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Cleanuparr.Infrastructure.Features.Notifications.Notifiarr;

public sealed class NotifiarrProxy : INotifiarrProxy
{
    private readonly ILogger<NotifiarrProxy> _logger;
    private readonly HttpClient _httpClient;

    private const string Url = "https://notifiarr.com/api/v1/notification/passthrough/";

    public NotifiarrProxy(ILogger<NotifiarrProxy> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient(Constants.HttpClientWithRetryName);
    }

    public async Task SendNotification(NotifiarrPayload payload, NotifiarrConfig config)
    {
        try
        {
            string content = JsonConvert.SerializeObject(payload, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            });
            
            _logger.LogTrace("sending notification to Notifiarr: {content}", content);
            
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"{Url}{config.ApiKey}");
            request.Method = HttpMethod.Post;
            request.Content = new StringContent(content, Encoding.UTF8, "application/json");
            
            using HttpResponseMessage response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException exception)
        {
            if (exception.StatusCode is null)
            {
                throw new NotifiarrException("unable to send notification", exception);
            }
            
            switch ((int)exception.StatusCode)
            {
                case 401:
                    throw new NotifiarrException("unable to send notification | API key is invalid");
                case 502:
                case 503:
                case 504:
                    throw new NotifiarrException("unable to send notification | service unavailable", exception);
                default:
                    throw new NotifiarrException("unable to send notification", exception);
            }
        }
    }
}