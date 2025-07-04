﻿using System.Net.Http.Headers;
using System.Text;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Cleanuparr.Shared.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Cleanuparr.Infrastructure.Features.Notifications.Apprise;

public sealed class AppriseProxy : IAppriseProxy
{
    private readonly HttpClient _httpClient;

    public AppriseProxy(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient(Constants.HttpClientWithRetryName);
    }

    public async Task SendNotification(ApprisePayload payload, AppriseConfig config)
    {
        try
        {
            string content = JsonConvert.SerializeObject(payload, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore
            });

            UriBuilder uriBuilder = new(config.Url.ToString());
            uriBuilder.Path = $"{uriBuilder.Path.TrimEnd('/')}/notify/{config.Key}";

            using HttpRequestMessage request = new(HttpMethod.Post, uriBuilder.Uri);
            request.Method = HttpMethod.Post;
            request.Content = new StringContent(content, Encoding.UTF8, "application/json");

            if (!string.IsNullOrEmpty(config.Url.UserInfo))
            {
                var byteArray = Encoding.ASCII.GetBytes(config.Url.UserInfo);
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            }

            using HttpResponseMessage response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException exception)
        {
            if (exception.StatusCode is null)
            {
                throw new AppriseException("Unable to send notification", exception);
            }

            switch ((int)exception.StatusCode)
            {
                case 401:
                    throw new AppriseException("Unable to send notification | API key is invalid");
                case 424:
                    throw new AppriseException("Your tags are not configured correctly", exception);
                case 502:
                case 503:
                case 504:
                    throw new AppriseException("Unable to send notification | service unavailable", exception);
                default:
                    throw new AppriseException("Unable to send notification", exception);
            }
        }
    }
}