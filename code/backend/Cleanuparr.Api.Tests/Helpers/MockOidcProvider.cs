using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace Cleanuparr.Api.Tests.Helpers;

/// <summary>
/// Mock OIDC identity provider for integration tests.
/// Generates RSA key pairs, creates valid/invalid ID tokens,
/// and provides mock discovery/token endpoint responses.
/// </summary>
public sealed class MockOidcProvider : IDisposable
{
    public const string DefaultIssuer = "https://mock-oidc-provider.test";
    public const string DefaultClientId = "cleanuparr-test-client";
    public const string DefaultClientSecret = "test-client-secret";
    public const string DefaultSubject = "mock-oidc-user-123";
    public const string DefaultUsername = "testuser";
    public const string DefaultEmail = "testuser@example.com";

    private readonly RSA _rsa;
    private readonly RsaSecurityKey _signingKey;
    private readonly SigningCredentials _signingCredentials;
    private readonly string _keyId;

    public MockOidcProvider()
    {
        _rsa = RSA.Create(2048);
        _keyId = Guid.NewGuid().ToString("N")[..8];
        _signingKey = new RsaSecurityKey(_rsa) { KeyId = _keyId };
        _signingCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256);
    }

    public RsaSecurityKey SigningKey => _signingKey;

    /// <summary>
    /// Creates a valid ID token with the given claims.
    /// </summary>
    public string CreateIdToken(
        string? subject = null,
        string? audience = null,
        string? issuer = null,
        string? nonce = null,
        string? preferredUsername = null,
        string? email = null,
        DateTime? issuedAt = null,
        DateTime? expiresAt = null,
        SigningCredentials? signingCredentials = null)
    {
        var now = issuedAt ?? DateTime.UtcNow;
        var expiry = expiresAt ?? now.AddHours(1);

        var claims = new List<Claim>
        {
            new("sub", subject ?? DefaultSubject),
            new("iat", new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
        };

        if (!string.IsNullOrEmpty(nonce))
        {
            claims.Add(new Claim("nonce", nonce));
        }

        if (!string.IsNullOrEmpty(preferredUsername))
        {
            claims.Add(new Claim("preferred_username", preferredUsername));
        }

        if (!string.IsNullOrEmpty(email))
        {
            claims.Add(new Claim("email", email));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = issuer ?? DefaultIssuer,
            Audience = audience ?? DefaultClientId,
            IssuedAt = now,
            Expires = expiry,
            NotBefore = now,
            SigningCredentials = signingCredentials ?? _signingCredentials
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);
        return handler.WriteToken(token);
    }

    /// <summary>
    /// Creates an expired ID token.
    /// </summary>
    public string CreateExpiredIdToken(string? nonce = null)
    {
        return CreateIdToken(
            nonce: nonce,
            issuedAt: DateTime.UtcNow.AddHours(-2),
            expiresAt: DateTime.UtcNow.AddHours(-1));
    }

    /// <summary>
    /// Creates an ID token signed with a different key (wrong signature).
    /// </summary>
    public string CreateWrongSignatureIdToken(string? nonce = null)
    {
        using var wrongRsa = RSA.Create(2048);
        var wrongKey = new RsaSecurityKey(wrongRsa) { KeyId = "wrong-key" };
        var wrongCredentials = new SigningCredentials(wrongKey, SecurityAlgorithms.RsaSha256);

        return CreateIdToken(
            nonce: nonce,
            signingCredentials: wrongCredentials);
    }

    /// <summary>
    /// Gets the JWKS (JSON Web Key Set) for this provider.
    /// </summary>
    public string GetJwksJson()
    {
        var parameters = _rsa.ExportParameters(false);

        var jwk = new Dictionary<string, object>
        {
            ["kty"] = "RSA",
            ["use"] = "sig",
            ["kid"] = _keyId,
            ["alg"] = "RS256",
            ["n"] = Base64UrlEncoder.Encode(parameters.Modulus!),
            ["e"] = Base64UrlEncoder.Encode(parameters.Exponent!)
        };

        var jwks = new Dictionary<string, object>
        {
            ["keys"] = new[] { jwk }
        };

        return JsonSerializer.Serialize(jwks);
    }

    /// <summary>
    /// Gets a mock OpenID Connect discovery document.
    /// </summary>
    public string GetDiscoveryDocument(string? issuer = null)
    {
        var iss = issuer ?? DefaultIssuer;
        var doc = new Dictionary<string, object>
        {
            ["issuer"] = iss,
            ["authorization_endpoint"] = $"{iss}/authorize",
            ["token_endpoint"] = $"{iss}/token",
            ["userinfo_endpoint"] = $"{iss}/userinfo",
            ["jwks_uri"] = $"{iss}/.well-known/jwks.json",
            ["response_types_supported"] = new[] { "code" },
            ["subject_types_supported"] = new[] { "public" },
            ["id_token_signing_alg_values_supported"] = new[] { "RS256" },
            ["scopes_supported"] = new[] { "openid", "profile", "email" },
            ["token_endpoint_auth_methods_supported"] = new[] { "client_secret_post", "client_secret_basic" },
            ["code_challenge_methods_supported"] = new[] { "S256" }
        };

        return JsonSerializer.Serialize(doc);
    }

    /// <summary>
    /// Creates a mock token endpoint response containing an ID token.
    /// </summary>
    public string CreateTokenResponse(
        string? subject = null,
        string? nonce = null,
        string? audience = null,
        string? issuer = null)
    {
        var idToken = CreateIdToken(
            subject: subject,
            nonce: nonce,
            audience: audience,
            issuer: issuer,
            preferredUsername: DefaultUsername,
            email: DefaultEmail);

        var response = new Dictionary<string, object>
        {
            ["id_token"] = idToken,
            ["access_token"] = $"mock-access-token-{Guid.NewGuid():N}",
            ["token_type"] = "Bearer",
            ["expires_in"] = 3600
        };

        return JsonSerializer.Serialize(response);
    }

    public void Dispose()
    {
        _rsa.Dispose();
    }
}
