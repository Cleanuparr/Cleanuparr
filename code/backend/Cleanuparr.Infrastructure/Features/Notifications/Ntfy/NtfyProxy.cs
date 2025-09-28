using System.Net.Http.Headers;
using System.Text;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Cleanuparr.Shared.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Cleanuparr.Infrastructure.Features.Notifications.Ntfy;

public sealed class NtfyProxy : INtfyProxy
{
    private readonly HttpClient _httpClient;

    public NtfyProxy(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient(Constants.HttpClientWithRetryName);
    }

    public async Task SendNotification(NtfyPayload payload, NtfyConfig config)
    {
        try
        {
            string content = JsonConvert.SerializeObject(payload, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore
            });

            var parsedUrl = config.Uri!;
            using HttpRequestMessage request = new(HttpMethod.Post, parsedUrl);
            request.Content = new StringContent(content, Encoding.UTF8, "application/json");

            // Set authentication headers based on configuration
            SetAuthenticationHeaders(request, config);

            using HttpResponseMessage response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException exception)
        {
            if (exception.StatusCode is null)
            {
                throw new NtfyException("Unable to send notification", exception);
            }

            switch ((int)exception.StatusCode)
            {
                case 400:
                    throw new NtfyException("Bad request - invalid topic or payload", exception);
                case 401:
                    throw new NtfyException("Unauthorized - invalid credentials", exception);
                case 413:
                    throw new NtfyException("Payload too large", exception);
                case 429:
                    throw new NtfyException("Rate limited - too many requests", exception);
                case 507:
                    throw new NtfyException("Insufficient storage on server", exception);
                default:
                    throw new NtfyException("Unable to send notification", exception);
            }
        }
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
