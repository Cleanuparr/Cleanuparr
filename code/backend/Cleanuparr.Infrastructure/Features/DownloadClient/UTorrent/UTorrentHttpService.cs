using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Cleanuparr.Domain.Entities.UTorrent.Request;
using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Persistence.Models.Configuration;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;

/// <summary>
/// Implementation of HTTP service for µTorrent Web UI API communication
/// Handles low-level HTTP requests and authentication token retrieval
/// </summary>
public class UTorrentHttpService : IUTorrentHttpService
{
    private readonly HttpClient _httpClient;
    private readonly DownloadClientConfig _config;
    private readonly ILogger<UTorrentHttpService> _logger;
    
    // Regex pattern to extract token from µTorrent Web UI HTML
    private static readonly Regex TokenRegex = new(@"<div[^>]*id=['""]token['""][^>]*>([^<]+)</div>", 
        RegexOptions.IgnoreCase);

    public UTorrentHttpService(
        HttpClient httpClient,
        DownloadClientConfig config,
        ILogger<UTorrentHttpService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string> SendRawRequestAsync(UTorrentRequest request, string guidCookie)
    {
        if (string.IsNullOrEmpty(guidCookie))
        {
            throw new UTorrentAuthenticationException("GUID cookie is required for API requests");
        }

        try
        {
            var queryString = request.ToQueryString();
            UriBuilder uriBuilder = new UriBuilder(_config.Url)
            {
                Query = queryString
            };
            uriBuilder.Path = $"{uriBuilder.Path.TrimEnd('/')}/gui/";
            
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, uriBuilder.Uri);
            httpRequest.Headers.Add("Cookie", guidCookie);
            
            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{_config.Username}:{_config.Password}"));
            httpRequest.Headers.Add("Authorization", $"Basic {credentials}");

            var response = await _httpClient.SendAsync(httpRequest);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("UTorrent API request failed: {StatusCode} - {Content}", response.StatusCode, errorContent);
                
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new UTorrentAuthenticationException("Authentication failed - invalid credentials or token expired");
                }

                throw new UTorrentException($"HTTP request failed: {response.StatusCode} - {errorContent}");
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            
            if (string.IsNullOrEmpty(jsonResponse))
            {
                throw new UTorrentException("Empty response received from µTorrent API");
            }

            return jsonResponse;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed for UTorrent API: {Action}", request.Action);
            throw new UTorrentException($"HTTP request failed: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "HTTP request timeout for UTorrent API: {Action}", request.Action);
            throw new UTorrentException($"HTTP request timeout: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<(string token, string guidCookie)> GetTokenAndCookieAsync()
    {
        try
        {
            UriBuilder uriBuilder = new UriBuilder(_config.Url);
            uriBuilder.Path = $"{uriBuilder.Path.TrimEnd('/')}/gui/token.html";
            
            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{_config.Username}:{_config.Password}"));

            var request = new HttpRequestMessage(HttpMethod.Get, uriBuilder.Uri);
            request.Headers.Add("Authorization", $"Basic {credentials}");

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to retrieve authentication token: {StatusCode} - {Content}", 
                    response.StatusCode, errorContent);
                
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new UTorrentAuthenticationException("Authentication failed - check username and password");
                }

                throw new UTorrentException($"Token retrieval failed: {response.StatusCode} - {errorContent}");
            }

            var html = await response.Content.ReadAsStringAsync();
            
            // Extract token from HTML
            var tokenMatch = TokenRegex.Match(html);
            if (!tokenMatch.Success)
            {
                _logger.LogError("Failed to extract token from HTML response: {Html}", html);
                throw new UTorrentAuthenticationException("Failed to extract authentication token from response");
            }

            var token = tokenMatch.Groups[1].Value;
            
            // Extract GUID from cookies
            var guidCookie = ExtractGuidCookie(response.Headers);
            
            if (string.IsNullOrEmpty(guidCookie))
            {
                throw new UTorrentAuthenticationException("Failed to extract GUID cookie from response");
            }

            return (token, guidCookie);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed while retrieving authentication token");
            throw new UTorrentAuthenticationException($"Token retrieval failed: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "HTTP request timeout while retrieving authentication token");
            throw new UTorrentAuthenticationException($"Token retrieval timeout: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Extracts the GUID cookie from HTTP response headers
    /// </summary>
    /// <param name="headers">HTTP response headers</param>
    /// <returns>GUID cookie string or empty string if not found</returns>
    private static string ExtractGuidCookie(System.Net.Http.Headers.HttpResponseHeaders headers)
    {
        if (!headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return string.Empty;
        }

        foreach (var cookie in cookies)
        {
            if (cookie.Contains("GUID="))
            {
                return cookie.Split(';')[0]; // Get just the GUID part, ignore expires, path, etc.
            }
        }

        return string.Empty;
    }
}
