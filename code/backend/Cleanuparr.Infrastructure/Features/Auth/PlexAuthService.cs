using System.Text.Json;
using System.Text.Json.Serialization;
using Cleanuparr.Shared.Helpers;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.Auth;

public sealed class PlexAuthService : IPlexAuthService
{
    private const string PlexApiBaseUrl = "https://plex.tv/api/v2";
    private const string PlexProduct = "Cleanuparr";

    private readonly HttpClient _httpClient;
    private readonly ILogger<PlexAuthService> _logger;
    private readonly string _clientIdentifier;

    public PlexAuthService(IHttpClientFactory httpClientFactory, ILogger<PlexAuthService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("PlexAuth");
        _logger = logger;
        _clientIdentifier = GetOrCreateClientIdentifier();
    }

    public async Task<PlexPinResult> RequestPin()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{PlexApiBaseUrl}/pins");
        AddPlexHeaders(request);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["strong"] = "true"
        });

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var pin = JsonSerializer.Deserialize<PlexPinResponse>(json);

        if (pin is null)
        {
            throw new InvalidOperationException("Failed to parse Plex PIN response");
        }

        var authUrl = $"https://app.plex.tv/auth#?clientID={Uri.EscapeDataString(_clientIdentifier)}&code={Uri.EscapeDataString(pin.Code)}&context%5Bdevice%5D%5Bproduct%5D={Uri.EscapeDataString(PlexProduct)}";

        return new PlexPinResult
        {
            PinId = pin.Id,
            PinCode = pin.Code,
            AuthUrl = authUrl
        };
    }

    public async Task<PlexPinCheckResult> CheckPin(int pinId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{PlexApiBaseUrl}/pins/{pinId}");
        AddPlexHeaders(request);

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            return new PlexPinCheckResult { Completed = false };
        }

        var json = await response.Content.ReadAsStringAsync();
        var pin = JsonSerializer.Deserialize<PlexPinResponse>(json);

        if (pin is null)
        {
            throw new InvalidOperationException("Failed to parse Plex PIN response");
        }

        return new PlexPinCheckResult
        {
            Completed = !string.IsNullOrEmpty(pin.AuthToken),
            AuthToken = pin.AuthToken
        };
    }

    public async Task<PlexAccountInfo> GetAccount(string authToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{PlexApiBaseUrl}/user");
        AddPlexHeaders(request);
        request.Headers.Add("X-Plex-Token", authToken);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var account = JsonSerializer.Deserialize<PlexAccountResponse>(json);

        if (account is null)
        {
            throw new InvalidOperationException("Failed to parse Plex account response");
        }

        return new PlexAccountInfo
        {
            AccountId = account.Id.ToString(),
            Username = account.Username,
            Email = account.Email
        };
    }

    private void AddPlexHeaders(HttpRequestMessage request)
    {
        request.Headers.Add("Accept", "application/json");
        request.Headers.Add("X-Plex-Client-Identifier", _clientIdentifier);
        request.Headers.Add("X-Plex-Product", PlexProduct);
    }

    private static string GetOrCreateClientIdentifier()
    {
        var path = Path.Combine(ConfigurationPathProvider.GetConfigPath(), "plex-client-id.txt");

        if (File.Exists(path))
        {
            return File.ReadAllText(path).Trim();
        }

        var clientId = Guid.NewGuid().ToString("N");

        var directory = Path.GetDirectoryName(path);
        if (directory is not null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, clientId);
        return clientId;
    }

    // JSON deserialization models
    private sealed class PlexPinResponse
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("authToken")]
        public string? AuthToken { get; set; }
    }

    private sealed class PlexAccountResponse
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string? Email { get; set; }
    }
}
