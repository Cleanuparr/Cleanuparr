using System.Net;
using System.Text;
using Cleanuparr.Shared.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Cleanuparr.Infrastructure.Features.Notifications.Telegram;

public sealed class TelegramProxy : ITelegramProxy
{
    private readonly HttpClient _httpClient;

    public TelegramProxy(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient(Constants.HttpClientWithRetryName);
    }

    public async Task SendNotification(TelegramPayload payload, string botToken)
    {
        string url = $"https://api.telegram.org/bot{botToken}/sendMessage";

        try
        {
            string content = JsonConvert.SerializeObject(payload, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore
            });

            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(content, Encoding.UTF8, "application/json");

            using HttpResponseMessage response = await _httpClient.SendAsync(request);
            
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            string body = await response.Content.ReadAsStringAsync();
            throw MapToException(response.StatusCode, body);
        }
        catch (HttpRequestException ex)
        {
            throw new TelegramException("Unable to reach Telegram API", ex);
        }
    }

    private static TelegramException MapToException(HttpStatusCode statusCode, string responseBody)
    {
        return statusCode switch
        {
            HttpStatusCode.BadRequest => new TelegramException($"Telegram rejected the request: {Truncate(responseBody)}"),
            HttpStatusCode.Unauthorized => new TelegramException("Telegram bot token is invalid"),
            HttpStatusCode.Forbidden => new TelegramException("Bot does not have permission to message the chat"),
            HttpStatusCode.TooManyRequests => new TelegramException("Rate limited by Telegram"),
            _ => new TelegramException($"Telegram API error ({(int)statusCode}): {Truncate(responseBody)}")
        };
    }

    private static string Truncate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        const int limit = 500;
        return value.Length <= limit ? value : value[..limit];
    }
}
