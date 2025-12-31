using System.Text;
using Cleanuparr.Shared.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Net;

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
        bool hasImage = !string.IsNullOrWhiteSpace(payload.PhotoUrl);
        bool captionFits = payload.Text.Length <= 1024;
        bool usePhoto = hasImage && captionFits;

        string endpoint = usePhoto ? "sendPhoto" : "sendMessage";
        string url = $"https://api.telegram.org/bot{botToken}/{endpoint}";

        string text = payload.Text;

        if (hasImage && !usePhoto)
        {
            text = $"{payload.Text}\n{BuildInvisibleImageLink(payload.PhotoUrl!)}";
        }

        object body = usePhoto
            ? new
            {
                chat_id = payload.ChatId,
                message_thread_id = payload.MessageThreadId,
                disable_notification = payload.DisableNotification,
                photo = payload.PhotoUrl,
                caption = text,
                parse_mode = "HTML"
            }
            : new
            {
                chat_id = payload.ChatId,
                message_thread_id = payload.MessageThreadId,
                disable_notification = payload.DisableNotification,
                text,
                parse_mode = "HTML",
                disable_web_page_preview = !hasImage
            };

        try
        {
            string content = JsonConvert.SerializeObject(body, new JsonSerializerSettings
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

            string bodyContent = await response.Content.ReadAsStringAsync();
            throw MapToException(response.StatusCode, bodyContent);
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

    private static string BuildInvisibleImageLink(string imageUrl)
    {
        // Zero-width space to force a preview without visible text as described in https://stackoverflow.com/a/55126912
        return $"<a href=\"{WebUtility.HtmlEncode(imageUrl)}\">&#8203;</a>";
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
