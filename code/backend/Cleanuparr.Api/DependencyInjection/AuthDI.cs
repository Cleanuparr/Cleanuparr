using Cleanuparr.Api.Auth;
using Cleanuparr.Infrastructure.Features.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Cleanuparr.Api.DependencyInjection;

public static class AuthDI
{
    private const string SmartScheme = "Smart";

    public static IServiceCollection AddAuthServices(this IServiceCollection services)
    {
        // Get the signing key from the JwtService
        var jwtService = new JwtService();
        var signingKey = jwtService.GetOrCreateSigningKey();

        services
            .AddAuthentication(SmartScheme)
            .AddPolicyScheme(SmartScheme, "JWT or API Key", options =>
            {
                // Route to the correct auth handler based on the request
                options.ForwardDefaultSelector = context =>
                {
                    if (context.Request.Headers.ContainsKey(ApiKeyAuthenticationDefaults.HeaderName) ||
                        context.Request.Query.ContainsKey(ApiKeyAuthenticationDefaults.QueryParameterName))
                    {
                        return ApiKeyAuthenticationDefaults.AuthenticationScheme;
                    }

                    // Check for Bearer token or SignalR access_token
                    if (context.Request.Headers.ContainsKey("Authorization") ||
                        (context.Request.Path.StartsWithSegments("/api/hubs") &&
                         context.Request.Query.ContainsKey("access_token")))
                    {
                        return JwtBearerDefaults.AuthenticationScheme;
                    }

                    // Fall through to trusted network handler (returns NoResult if disabled)
                    return TrustedNetworkAuthenticationDefaults.AuthenticationScheme;
                };
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = "Cleanuparr",
                    ValidateAudience = true,
                    ValidAudience = "Cleanuparr",
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(signingKey),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

                // Support SignalR token via query string
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;

                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/api/hubs"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            })
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationDefaults.AuthenticationScheme, _ => { })
            .AddScheme<AuthenticationSchemeOptions, TrustedNetworkAuthenticationHandler>(
                TrustedNetworkAuthenticationDefaults.AuthenticationScheme, _ => { });

        services.AddAuthorization(options =>
        {
            var defaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            options.DefaultPolicy = defaultPolicy;
            options.FallbackPolicy = defaultPolicy;
        });

        return services;
    }
}
