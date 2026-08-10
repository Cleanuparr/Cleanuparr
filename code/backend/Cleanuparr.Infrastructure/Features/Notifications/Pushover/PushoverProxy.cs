using System.Net;
using System.Text.Json;
using Cleanuparr.Infrastructure.Json;
using Cleanuparr.Shared.Helpers;

namespace Cleanuparr.Infrastructure.Features.Notifications.Pushover;

public sealed class PushoverProxy : IPushoverProxy
{
    private const string ApiUrl = "https://api.pushover.net/1/messages.json";
    private readonly HttpClient _httpClient;

    public PushoverProxy(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient(Constants.HttpClientWithRetryName);
    }

    public async Task SendNotification(PushoverPayload payload)
    {
        try
        {
            var formData = new Dictionary<string, string>
            {
                ["token"] = payload.Token,
                ["user"] = payload.User,
                ["message"] = payload.Message,
                ["priority"] = payload.Priority.ToString()
            };

            if (!string.IsNullOrWhiteSpace(payload.Title))
                formData["title"] = payload.Title;

            if (!string.IsNullOrWhiteSpace(payload.Device))
                formData["device"] = payload.Device;

            if (!string.IsNullOrWhiteSpace(payload.Sound))
                formData["sound"] = payload.Sound;

            // Emergency priority requires retry and expire
            if (payload.Priority == 2)
            {
                if (payload.Retry.HasValue)
                    formData["retry"] = payload.Retry.Value.ToString();
                if (payload.Expire.HasValue)
                    formData["expire"] = payload.Expire.Value.ToString();
            }

            if (!string.IsNullOrWhiteSpace(payload.Tags))
                formData["tags"] = payload.Tags;

            using var content = new FormUrlEncodedContent(formData);
            using var response = await _httpClient.PostAsync(ApiUrl, content);

            var responseBody = await response.Content.ReadAsStringAsync();
            PushoverResponse? pushoverResponse = ReadResponse(responseBody);

            if (!response.IsSuccessStatusCode || pushoverResponse?.IsSuccess != true)
            {
                var errorMessage = pushoverResponse?.Errors?.FirstOrDefault()
                    ?? $"Pushover API error: {response.StatusCode}";

                throw response.StatusCode switch
                {
                    HttpStatusCode.BadRequest => new PushoverException($"Bad request: {errorMessage}"),
                    HttpStatusCode.Unauthorized => new PushoverException("Invalid API token or user key"),
                    (HttpStatusCode)429 => new PushoverException("Rate limit exceeded - monthly quota reached"),
                    _ => new PushoverException($"Failed to send notification: {errorMessage}")
                };
            }
        }
        catch (PushoverException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new PushoverException("Unable to connect to Pushover API", ex);
        }
    }

    /// <summary>
    /// Reads the body of a response from Pushover.
    /// </summary>
    /// <remarks>
    /// A proxy or a gateway can send an error page that is not JSON. A body that
    /// this method cannot read gives null, and the status code of the response
    /// then gives the error message.
    /// </remarks>
    private static PushoverResponse? ReadResponse(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PushoverResponse>(responseBody, CleanuparrJsonOptions.ExternalApiRead);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
