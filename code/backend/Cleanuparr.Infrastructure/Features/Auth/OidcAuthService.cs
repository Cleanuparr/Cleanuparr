using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Cleanuparr.Infrastructure.Features.Auth;

public sealed class OidcAuthService : IOidcAuthService
{
    private const int MaxPendingFlows = 100;
    private const int MaxOneTimeCodes = 100;
    private static readonly TimeSpan FlowStateExpiry = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan OneTimeCodeExpiry = TimeSpan.FromSeconds(30);

    private static readonly ConcurrentDictionary<string, OidcFlowState> PendingFlows = new();
    private static readonly ConcurrentDictionary<string, OidcOneTimeCodeEntry> OneTimeCodes = new();
    private static readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> ConfigManagers = new();
    
    // Reference held to prevent GC collection; the timer fires CleanupExpiredEntries every minute
    #pragma warning disable IDE0052
    private static readonly Timer CleanupTimer = new(CleanupExpiredEntries, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    #pragma warning restore IDE0052

    private readonly HttpClient _httpClient;
    private readonly UsersContext _usersContext;
    private readonly ILogger<OidcAuthService> _logger;

    public OidcAuthService(
        IHttpClientFactory httpClientFactory,
        UsersContext usersContext,
        ILogger<OidcAuthService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("OidcAuth");
        _usersContext = usersContext;
        _logger = logger;
    }

    public async Task<OidcAuthorizationResult> StartAuthorization(string redirectUri, string? initiatorUserId = null)
    {
        var oidcConfig = await GetOidcConfig();

        if (!oidcConfig.Enabled)
        {
            throw new InvalidOperationException("OIDC is not enabled");
        }

        if (PendingFlows.Count >= MaxPendingFlows)
        {
            throw new InvalidOperationException("Too many pending OIDC flows. Please try again later.");
        }

        var discovery = await GetDiscoveryDocument(oidcConfig.IssuerUrl);

        var state = GenerateRandomString();
        var nonce = GenerateRandomString();
        var codeVerifier = GenerateRandomString();
        var codeChallenge = ComputeCodeChallenge(codeVerifier);

        var flowState = new OidcFlowState
        {
            State = state,
            Nonce = nonce,
            CodeVerifier = codeVerifier,
            RedirectUri = redirectUri,
            InitiatorUserId = initiatorUserId,
            CreatedAt = DateTime.UtcNow
        };

        if (!PendingFlows.TryAdd(state, flowState))
        {
            throw new InvalidOperationException("Failed to store OIDC flow state");
        }

        var authUrl = BuildAuthorizationUrl(
            discovery.AuthorizationEndpoint,
            oidcConfig.ClientId,
            redirectUri,
            oidcConfig.Scopes,
            state,
            nonce,
            codeChallenge);

        _logger.LogDebug("OIDC authorization started with state {State}", state);

        return new OidcAuthorizationResult
        {
            AuthorizationUrl = authUrl,
            State = state
        };
    }

    public async Task<OidcCallbackResult> HandleCallback(string code, string state, string redirectUri)
    {
        if (!PendingFlows.TryGetValue(state, out var flowState))
        {
            _logger.LogWarning("OIDC callback with invalid or expired state: {State}", state);
            return new OidcCallbackResult
            {
                Success = false,
                Error = "Invalid or expired OIDC state"
            };
        }

        if (DateTime.UtcNow - flowState.CreatedAt > FlowStateExpiry)
        {
            PendingFlows.TryRemove(state, out _);
            _logger.LogWarning("OIDC flow state expired for state: {State}", state);
            return new OidcCallbackResult
            {
                Success = false,
                Error = "OIDC flow has expired"
            };
        }

        if (flowState.RedirectUri != redirectUri)
        {
            _logger.LogWarning("OIDC callback redirect URI mismatch");
            return new OidcCallbackResult
            {
                Success = false,
                Error = "Redirect URI mismatch"
            };
        }

        // Validation passed — consume the state
        PendingFlows.TryRemove(state, out _);

        var oidcConfig = await GetOidcConfig();
        var discovery = await GetDiscoveryDocument(oidcConfig.IssuerUrl);

        // Exchange authorization code for tokens
        var tokenResponse = await ExchangeCodeForTokens(
            discovery.TokenEndpoint,
            code,
            flowState.CodeVerifier,
            redirectUri,
            oidcConfig.ClientId,
            oidcConfig.ClientSecret);

        if (tokenResponse is null)
        {
            return new OidcCallbackResult
            {
                Success = false,
                Error = "Failed to exchange authorization code"
            };
        }

        // Validate the ID token
        var validatedToken = await ValidateIdToken(
            tokenResponse.IdToken,
            oidcConfig,
            discovery,
            flowState.Nonce);

        if (validatedToken is null)
        {
            return new OidcCallbackResult
            {
                Success = false,
                Error = "ID token validation failed"
            };
        }

        var subject = validatedToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        var preferredUsername = validatedToken.Claims.FirstOrDefault(c => c.Type == "preferred_username")?.Value;
        var email = validatedToken.Claims.FirstOrDefault(c => c.Type == "email")?.Value;

        if (string.IsNullOrEmpty(subject))
        {
            return new OidcCallbackResult
            {
                Success = false,
                Error = "ID token missing 'sub' claim"
            };
        }

        _logger.LogInformation("OIDC authentication successful for subject: {Subject}", subject);

        return new OidcCallbackResult
        {
            Success = true,
            Subject = subject,
            PreferredUsername = preferredUsername,
            Email = email,
            InitiatorUserId = flowState.InitiatorUserId
        };
    }

    public string StoreOneTimeCode(string accessToken, string refreshToken, int expiresIn)
    {
        // Clean up if at capacity
        if (OneTimeCodes.Count >= MaxOneTimeCodes)
        {
            CleanupExpiredOneTimeCodes();

            // If still at capacity after cleanup, evict oldest entries
            while (OneTimeCodes.Count >= MaxOneTimeCodes)
            {
                var oldest = OneTimeCodes.OrderBy(x => x.Value.CreatedAt).FirstOrDefault();
                if (oldest.Key is not null)
                {
                    OneTimeCodes.TryRemove(oldest.Key, out _);
                }
                else
                {
                    break;
                }
            }
        }

        var entry = new OidcOneTimeCodeEntry
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = expiresIn,
            CreatedAt = DateTime.UtcNow
        };

        // Retry with new codes on collision
        for (var i = 0; i < 3; i++)
        {
            var code = GenerateRandomString();
            if (OneTimeCodes.TryAdd(code, entry))
            {
                return code;
            }
        }

        throw new InvalidOperationException("Failed to generate a unique one-time code");
    }

    public OidcTokenExchangeResult? ExchangeOneTimeCode(string code)
    {
        if (!OneTimeCodes.TryRemove(code, out var entry))
        {
            return null;
        }

        if (DateTime.UtcNow - entry.CreatedAt > OneTimeCodeExpiry)
        {
            return null;
        }

        return new OidcTokenExchangeResult
        {
            AccessToken = entry.AccessToken,
            RefreshToken = entry.RefreshToken,
            ExpiresIn = entry.ExpiresIn
        };
    }

    private async Task<OidcConfig> GetOidcConfig()
    {
        var user = await _usersContext.Users.AsNoTracking().FirstOrDefaultAsync();
        return user?.Oidc ?? new OidcConfig();
    }

    private async Task<OpenIdConnectConfiguration> GetDiscoveryDocument(string issuerUrl)
    {
        var metadataAddress = issuerUrl.TrimEnd('/') + "/.well-known/openid-configuration";

        var configManager = ConfigManagers.GetOrAdd(issuerUrl, _ =>
        {
            var isLocalhost = Uri.TryCreate(issuerUrl, UriKind.Absolute, out var uri) &&
                              uri.Host is "localhost" or "127.0.0.1" or "::1" or "[::1]";
            return new ConfigurationManager<OpenIdConnectConfiguration>(
                metadataAddress,
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever(_httpClient) { RequireHttps = !isLocalhost });
        });

        return await configManager.GetConfigurationAsync();
    }

    private async Task<OidcTokenResponse?> ExchangeCodeForTokens(
        string tokenEndpoint,
        string code,
        string codeVerifier,
        string redirectUri,
        string clientId,
        string clientSecret)
    {
        var parameters = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = clientId,
            ["code_verifier"] = codeVerifier
        };

        if (!string.IsNullOrEmpty(clientSecret))
        {
            parameters["client_secret"] = clientSecret;
        }

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
            {
                Content = new FormUrlEncodedContent(parameters)
            };

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("OIDC token exchange failed with status {Status}: {Body}",
                    response.StatusCode, errorBody);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<OidcTokenResponse>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OIDC token exchange failed");
            return null;
        }
    }

    private async Task<JwtSecurityToken?> ValidateIdToken(
        string idToken,
        OidcConfig oidcConfig,
        OpenIdConnectConfiguration discovery,
        string expectedNonce)
    {
        var handler = new JwtSecurityTokenHandler();

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = oidcConfig.IssuerUrl.TrimEnd('/'),
            ValidateAudience = true,
            ValidAudience = oidcConfig.ClientId,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = discovery.SigningKeys,
            ClockSkew = TimeSpan.FromMinutes(2)
        };

        try
        {
            handler.ValidateToken(idToken, validationParameters, out var validatedSecurityToken);
            var jwtToken = (JwtSecurityToken)validatedSecurityToken;

            return ValidateNonce(jwtToken, expectedNonce) ? jwtToken : null;
        }
        catch (SecurityTokenSignatureKeyNotFoundException)
        {
            // Try refreshing the configuration (JWKS key rotation)
            _logger.LogInformation("OIDC signing key not found, refreshing configuration");

            if (ConfigManagers.TryGetValue(oidcConfig.IssuerUrl, out var configManager))
            {
                configManager.RequestRefresh();
                var refreshedConfig = await configManager.GetConfigurationAsync();
                validationParameters.IssuerSigningKeys = refreshedConfig.SigningKeys;

                try
                {
                    handler.ValidateToken(idToken, validationParameters, out var retryToken);
                    var jwtRetryToken = (JwtSecurityToken)retryToken;

                    return ValidateNonce(jwtRetryToken, expectedNonce) ? jwtRetryToken : null;
                }
                catch (Exception retryEx)
                {
                    _logger.LogError(retryEx, "OIDC ID token validation failed after key refresh");
                    return null;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OIDC ID token validation failed");
            return null;
        }
    }

    private static string BuildAuthorizationUrl(
        string authorizationEndpoint,
        string clientId,
        string redirectUri,
        string scopes,
        string state,
        string nonce,
        string codeChallenge)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = scopes,
            ["state"] = state,
            ["nonce"] = nonce,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256"
        };

        var queryString = string.Join("&",
            queryParams.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        return $"{authorizationEndpoint}?{queryString}";
    }

    private bool ValidateNonce(JwtSecurityToken jwtToken, string expectedNonce)
    {
        var tokenNonce = jwtToken.Claims.FirstOrDefault(c => c.Type == "nonce")?.Value;
        if (tokenNonce == expectedNonce) return true;

        _logger.LogWarning("OIDC ID token nonce mismatch. Expected: {Expected}, Got: {Got}",
            expectedNonce, tokenNonce);
        return false;
    }

    private static string GenerateRandomString()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string ComputeCodeChallenge(string codeVerifier)
    {
        var bytes = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static void CleanupExpiredEntries(object? state)
    {
        var flowCutoff = DateTime.UtcNow - FlowStateExpiry;
        foreach (var kvp in PendingFlows)
        {
            if (kvp.Value.CreatedAt < flowCutoff)
            {
                PendingFlows.TryRemove(kvp.Key, out _);
            }
        }

        CleanupExpiredOneTimeCodes();
    }

    private static void CleanupExpiredOneTimeCodes()
    {
        var codeCutoff = DateTime.UtcNow - OneTimeCodeExpiry;
        foreach (var kvp in OneTimeCodes)
        {
            if (kvp.Value.CreatedAt < codeCutoff)
            {
                OneTimeCodes.TryRemove(kvp.Key, out _);
            }
        }
    }

    /// <summary>
    /// Clears the cached OIDC discovery configuration. Used when issuer URL changes.
    /// </summary>
    public static void ClearDiscoveryCache()
    {
        ConfigManagers.Clear();
    }

    private sealed class OidcFlowState
    {
        public required string State { get; init; }
        public required string Nonce { get; init; }
        public required string CodeVerifier { get; init; }
        public required string RedirectUri { get; init; }
        public string? InitiatorUserId { get; init; }
        public required DateTime CreatedAt { get; init; }
    }

    private sealed class OidcOneTimeCodeEntry
    {
        public required string AccessToken { get; init; }
        public required string RefreshToken { get; init; }
        public required int ExpiresIn { get; init; }
        public required DateTime CreatedAt { get; init; }
    }

    private sealed class OidcTokenResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("id_token")]
        public string IdToken { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;
    }
}
