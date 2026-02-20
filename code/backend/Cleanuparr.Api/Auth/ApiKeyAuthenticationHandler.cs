using System.Security.Claims;
using System.Text.Encodings.Web;
using Cleanuparr.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Cleanuparr.Api.Auth;

public static class ApiKeyAuthenticationDefaults
{
    public const string AuthenticationScheme = "ApiKey";
    public const string HeaderName = "X-Api-Key";
    public const string QueryParameterName = "apikey";
}

public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Try header first, then query string
        string? apiKey = null;

        if (Request.Headers.TryGetValue(ApiKeyAuthenticationDefaults.HeaderName, out var headerValue))
        {
            apiKey = headerValue.ToString();
        }
        else if (Request.Query.TryGetValue(ApiKeyAuthenticationDefaults.QueryParameterName, out var queryValue))
        {
            apiKey = queryValue.ToString();
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return AuthenticateResult.NoResult();
        }

        await using var usersContext = UsersContext.CreateStaticInstance();
        var user = await usersContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.ApiKey == apiKey && u.SetupCompleted);

        if (user is null)
        {
            return AuthenticateResult.Fail("Invalid API key");
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim("auth_method", "apikey")
        };

        var identity = new ClaimsIdentity(claims, ApiKeyAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, ApiKeyAuthenticationDefaults.AuthenticationScheme);

        return AuthenticateResult.Success(ticket);
    }
}
