using System.Net;
using Cleanuparr.Infrastructure.Services;
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
        
        if (!configStore.TryGetConfiguration(name, out HttpClientConfig? config))
            return;

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

    private void ConfigureHandler(HttpMessageHandlerBuilder builder, HttpClientConfig config)
    {
        using var scope = _scopeFactory.CreateScope();
        var certValidationService = scope.ServiceProvider.GetRequiredService<CertificateValidationService>();
        
        switch (config.Type)
        {
            case HttpClientType.WithRetry:
                builder.PrimaryHandler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (sender, certificate, chain, policy) =>
                        certValidationService.ShouldByPassValidationError(config.CertificateValidationType, sender, certificate, chain, policy)
                };
                break;

            case HttpClientType.Deluge:
                builder.PrimaryHandler = new HttpClientHandler
                {
                    AllowAutoRedirect = config.AllowAutoRedirect,
                    UseCookies = true,
                    CookieContainer = new CookieContainer(),
                    AutomaticDecompression = config.AutomaticDecompression,
                    ServerCertificateCustomValidationCallback = (sender, certificate, chain, policy) =>
                        certValidationService.ShouldByPassValidationError(config.CertificateValidationType, sender, certificate, chain, policy),
                };
                break;

            case HttpClientType.Default:
            default:
                // Use default handler with certificate validation
                var defaultHandler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (sender, certificate, chain, policy) =>
                        certValidationService.ShouldByPassValidationError(config.CertificateValidationType, sender, certificate, chain, policy)
                };
                builder.PrimaryHandler = defaultHandler;
                break;
        }
    }

    private void AddRetryPolicy(HttpMessageHandlerBuilder builder, RetryConfig retryConfig)
    {
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError();

        if (retryConfig.ExcludeUnauthorized)
        {
            retryPolicy = retryPolicy.OrResult(response => 
                !response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.Unauthorized);
        }
        else
        {
            retryPolicy = retryPolicy.OrResult(response => !response.IsSuccessStatusCode);
        }

        var policy = retryPolicy.WaitAndRetryAsync(
            retryConfig.MaxRetries, 
            retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
        );

        builder.AdditionalHandlers.Add(new PolicyHttpMessageHandler(policy));
    }

    public void Configure(HttpClientFactoryOptions options)
    {
        // This is called for unnamed clients - we don't need to do anything here
    }
} 