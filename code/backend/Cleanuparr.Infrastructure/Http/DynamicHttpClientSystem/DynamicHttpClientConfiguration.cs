using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Services;
using Cleanuparr.Shared.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;

namespace Cleanuparr.Infrastructure.Http.DynamicHttpClientSystem;

/// <summary>
/// Dynamic configuration handler - this configures HttpClients on-demand based on stored configurations
/// </summary>
public class DynamicHttpClientConfiguration : IConfigureNamedOptions<HttpClientFactoryOptions>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DynamicHttpClientConfiguration(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public void Configure(string name, HttpClientFactoryOptions options)
    {
        using var scope = _scopeFactory.CreateScope();
        var configStore = scope.ServiceProvider.GetRequiredService<IHttpClientConfigStore>();

        // The store keys on the bare name, IHttpClientFactory sees "name#generation".
        string baseName = DynamicHttpClientFactory.StripVersion(name);

        if (!configStore.TryGetConfiguration(baseName, out HttpClientConfig? config))
            return;

        if (config.SendUserAgent)
        {
            options.HttpClientActions.Add(ApplyUserAgent);
        }

        if (config.Type is HttpClientType.Plain)
        {
            return;
        }

        // Configure the HttpClient
        options.HttpClientActions.Add(httpClient =>
        {
            httpClient.Timeout = TimeSpan.FromSeconds(config.Timeout);
        });

        // Configure the HttpMessageHandler based on type
        options.HttpMessageHandlerBuilderActions.Add(builder =>
        {
            ConfigureHandler(builder, config);
            
            // Add retry policy if configured
            if (config.RetryConfig != null)
            {
                AddRetryPolicy(builder, config.RetryConfig);
            }
        });
    }

    internal static void ApplyUserAgent(HttpClient client)
    {
        client.DefaultRequestHeaders.UserAgent.ParseAdd(AppUserAgent.Value);
    }

    private void ConfigureHandler(HttpMessageHandlerBuilder builder, HttpClientConfig config)
    {
        using var scope = _scopeFactory.CreateScope();
        var certValidationService = scope.ServiceProvider.GetRequiredService<CertificateValidationService>();

        bool ByPassValidation(HttpRequestMessage sender, X509Certificate2? certificate, X509Chain? chain, SslPolicyErrors policy) =>
            certValidationService.ShouldByPassValidationError(config.CertificateValidationType, sender, certificate, chain, policy);

        HttpClientHandler handler = config.Type switch
        {
            HttpClientType.Deluge => new HttpClientHandler
            {
                AllowAutoRedirect = config.AllowAutoRedirect,
                UseCookies = true,
                CookieContainer = new CookieContainer(),
                AutomaticDecompression = config.AutomaticDecompression,
                ServerCertificateCustomValidationCallback = ByPassValidation,
            },

            // uTorrent's WebUI requires the GUID cookie+token pair set manually by UTorrentHttpService
            // UseCookies=false prevents .NET's CookieContainer from injecting a competing Cookie header.
            HttpClientType.UTorrent => new HttpClientHandler
            {
                UseCookies = false,
                ServerCertificateCustomValidationCallback = ByPassValidation,
            },

            // WithRetry and Default use the same handler shape.
            _ => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = ByPassValidation,
            },
        };

        ApplyHttpAuth(builder.AdditionalHandlers, handler, config);

        builder.PrimaryHandler = handler;
    }

    /// <summary>
    /// Digest goes on <see cref="HttpClientHandler.Credentials"/>, Basic on a delegating handler.
    /// </summary>
    internal static void ApplyHttpAuth(ICollection<DelegatingHandler> additionalHandlers, HttpClientHandler handler, HttpClientConfig config)
    {
        if (config.HttpAuthCredential is null)
        {
            return;
        }

        if (config.HttpAuthType == HttpAuthType.Digest)
        {
            Uri baseUri = config.BaseUri ?? throw new InvalidOperationException("Digest authentication requires a base URI");

            // The "Digest" key blocks a fallback to NTLM/Negotiate for this authority.
            handler.Credentials = new CredentialCache { { baseUri, "Digest", config.HttpAuthCredential } };
            handler.PreAuthenticate = true;
            return;
        }

        additionalHandlers.Add(new BasicAuthHttpMessageHandler(config.HttpAuthCredential));
    }

    private void AddRetryPolicy(HttpMessageHandlerBuilder builder, RetryConfig retryConfig)
    {
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError();

        retryPolicy = retryPolicy.OrResult(response => IsRetryable(response, retryConfig.ExcludeUnauthorized));

        var policy = retryPolicy.WaitAndRetryAsync(
            retryConfig.MaxRetries, 
            retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
        );

        builder.AdditionalHandlers.Add(new PolicyHttpMessageHandler(policy));
    }

    internal static bool IsRetryable(HttpResponseMessage response, bool excludeUnauthorized)
    {
        if (response.IsSuccessStatusCode)
        {
            return false;
        }

        if (response.StatusCode is HttpStatusCode.Conflict)
        {
            return false;
        }

        return !excludeUnauthorized || response.StatusCode != HttpStatusCode.Unauthorized;
    }

    public void Configure(HttpClientFactoryOptions options)
    {
        // Never called: the factory prefers the named overload.
        // Unnamed clients reach that one with an empty name.
    }
} 