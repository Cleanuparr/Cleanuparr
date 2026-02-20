using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Cleanuparr.Persistence.Models.Auth;
using Cleanuparr.Shared.Helpers;
using Microsoft.IdentityModel.Tokens;

namespace Cleanuparr.Infrastructure.Features.Auth;

public sealed class JwtService : IJwtService
{
    private const string Issuer = "Cleanuparr";
    private const string Audience = "Cleanuparr";
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan LoginTokenLifetime = TimeSpan.FromMinutes(5);

    private readonly byte[] _signingKey;

    public JwtService()
    {
        _signingKey = GetOrCreateSigningKey();
    }

    public string GenerateAccessToken(User user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim("token_type", "access")
        };

        return GenerateToken(claims, AccessTokenLifetime);
    }

    public string GenerateLoginToken(Guid userId)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("token_type", "login")
        };

        return GenerateToken(claims, LoginTokenLifetime);
    }

    public string GenerateRefreshToken()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    public ClaimsPrincipal? ValidateAccessToken(string token)
    {
        var principal = ValidateToken(token);
        if (principal is null) return null;

        var tokenType = principal.FindFirst("token_type")?.Value;
        return tokenType == "access" ? principal : null;
    }

    public Guid? ValidateLoginToken(string token)
    {
        var principal = ValidateToken(token);
        if (principal is null) return null;

        var tokenType = principal.FindFirst("token_type")?.Value;
        if (tokenType != "login") return null;

        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    public byte[] GetOrCreateSigningKey()
    {
        var keyPath = Path.Combine(ConfigurationPathProvider.GetConfigPath(), "jwt-key.bin");

        if (File.Exists(keyPath))
        {
            return File.ReadAllBytes(keyPath);
        }

        var key = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(key);

        var directory = Path.GetDirectoryName(keyPath);
        if (directory is not null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(keyPath, key);
        return key;
    }

    private string GenerateToken(Claim[] claims, TimeSpan lifetime)
    {
        var key = new SymmetricSecurityKey(_signingKey);
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(lifetime),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private ClaimsPrincipal? ValidateToken(string token)
    {
        var key = new SymmetricSecurityKey(_signingKey);
        var handler = new JwtSecurityTokenHandler();

        try
        {
            return handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = Issuer,
                ValidateAudience = true,
                ValidAudience = Audience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);
        }
        catch
        {
            return null;
        }
    }
}
