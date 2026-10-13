using System.Net;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Http.DynamicHttpClientSystem;
using Cleanuparr.Infrastructure.Services;
using Cleanuparr.Shared.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Http;

public sealed class DynamicHttpClientConfigurationTests
{
    [Theory]
    [InlineData(HttpClientType.Default)]
    [InlineData(HttpClientType.WithRetry)]
    [InlineData(HttpClientType.Deluge)]
    [InlineData(HttpClientType.UTorrent)]
    public void ApplyHttpAuth_InDigestMode_SetsCredentialsAndPreAuthenticate(HttpClientType clientType)
    {
        using HttpClientHandler handler = new();
        List<DelegatingHandler> additionalHandlers = [];
        HttpClientConfig config = new()
        {
            Type = clientType,
            HttpAuthType = HttpAuthType.Digest,
            HttpAuthCredential = new NetworkCredential("user", "pass"),
            BaseUri = new Uri("http://localhost:8080"),
        };

        DynamicHttpClientConfiguration.ApplyHttpAuth(additionalHandlers, handler, config);

        handler.PreAuthenticate.ShouldBeTrue();
        CredentialCache cache = handler.Credentials.ShouldBeOfType<CredentialCache>();
        cache.GetCredential(config.BaseUri, "Digest").ShouldNotBeNull();
        additionalHandlers.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(HttpClientType.Default)]
    [InlineData(HttpClientType.WithRetry)]
    [InlineData(HttpClientType.Deluge)]
    [InlineData(HttpClientType.UTorrent)]
    public void ApplyHttpAuth_InBasicMode_AddsDelegatingHandlerInstead(HttpClientType clientType)
    {
        using HttpClientHandler handler = new();
        List<DelegatingHandler> additionalHandlers = [];
        HttpClientConfig config = new()
        {
            Type = clientType,
            HttpAuthType = HttpAuthType.Basic,
            HttpAuthCredential = new NetworkCredential("user", "pass"),
            BaseUri = new Uri("http://localhost:8080"),
        };

        DynamicHttpClientConfiguration.ApplyHttpAuth(additionalHandlers, handler, config);

        handler.Credentials.ShouldBeNull();
        handler.PreAuthenticate.ShouldBeFalse();
        additionalHandlers.ShouldHaveSingleItem();
        additionalHandlers[0].ShouldBeOfType<BasicAuthHttpMessageHandler>();
    }

    [Fact]
    public void ApplyHttpAuth_WithNoCredential_DoesNothing()
    {
        using HttpClientHandler handler = new();
        List<DelegatingHandler> additionalHandlers = [];
        HttpClientConfig config = new()
        {
            Type = HttpClientType.WithRetry,
            HttpAuthType = HttpAuthType.Digest,
            HttpAuthCredential = null,
            BaseUri = new Uri("http://localhost:8080"),
        };

        DynamicHttpClientConfiguration.ApplyHttpAuth(additionalHandlers, handler, config);

        handler.Credentials.ShouldBeNull();
        handler.PreAuthenticate.ShouldBeFalse();
        additionalHandlers.ShouldBeEmpty();
    }

    [Fact]
    public void IsRetryable_ShouldReturnFalse_WhenResponseIsConflict()
    {
        using HttpResponseMessage response = new(HttpStatusCode.Conflict);

        DynamicHttpClientConfiguration.IsRetryable(response, excludeUnauthorized: true).ShouldBeFalse();
        DynamicHttpClientConfiguration.IsRetryable(response, excludeUnauthorized: false).ShouldBeFalse();
    }

    [Fact]
    public void IsRetryable_ShouldReturnFalse_WhenResponseIsSuccessful()
    {
        using HttpResponseMessage response = new(HttpStatusCode.OK);

        DynamicHttpClientConfiguration.IsRetryable(response, excludeUnauthorized: true).ShouldBeFalse();
    }

    [Fact]
    public void IsRetryable_ShouldRespectExcludeUnauthorized()
    {
        using HttpResponseMessage response = new(HttpStatusCode.Unauthorized);

        DynamicHttpClientConfiguration.IsRetryable(response, excludeUnauthorized: true).ShouldBeFalse();
        DynamicHttpClientConfiguration.IsRetryable(response, excludeUnauthorized: false).ShouldBeTrue();
    }

    [Fact]
    public void IsRetryable_ShouldReturnTrue_WhenResponseIsServerError()
    {
        using HttpResponseMessage response = new(HttpStatusCode.InternalServerError);

        DynamicHttpClientConfiguration.IsRetryable(response, excludeUnauthorized: true).ShouldBeTrue();
    }

    [Fact]
    public void Configure_WithAVersionedName_ResolvesTheBaseConfiguration()
    {
        HttpClientConfigStore store = new();
        store.AddConfiguration("client", new HttpClientConfig { Type = HttpClientType.Plain, SendUserAgent = true });

        ServiceCollection services = new();
        services.AddSingleton<IHttpClientConfigStore>(store);
        using ServiceProvider provider = services.BuildServiceProvider();

        DynamicHttpClientConfiguration configuration = new(provider.GetRequiredService<IServiceScopeFactory>());
        HttpClientFactoryOptions options = new();

        configuration.Configure("client#1", options);

        options.HttpClientActions.ShouldHaveSingleItem();
    }

    [Fact]
    public void Configure_WithAnUnknownBaseName_DoesNothing()
    {
        HttpClientConfigStore store = new();

        ServiceCollection services = new();
        services.AddSingleton<IHttpClientConfigStore>(store);
        using ServiceProvider provider = services.BuildServiceProvider();

        DynamicHttpClientConfiguration configuration = new(provider.GetRequiredService<IServiceScopeFactory>());
        HttpClientFactoryOptions options = new();

        configuration.Configure("client#1", options);

        options.HttpClientActions.ShouldBeEmpty();
    }

    [Fact]
    public void ApplyUserAgent_ShouldSetTheProductToken()
    {
        using HttpClient client = new();

        DynamicHttpClientConfiguration.ApplyUserAgent(client);

        client.DefaultRequestHeaders.UserAgent.ToString().ShouldBe(AppUserAgent.Value);
    }

    [Fact]
    public void ApplyHttpAuth_DigestWithNullBaseUri_Throws()
    {
        using HttpClientHandler handler = new();
        List<DelegatingHandler> additionalHandlers = [];
        HttpClientConfig config = new()
        {
            Type = HttpClientType.WithRetry,
            HttpAuthType = HttpAuthType.Digest,
            HttpAuthCredential = new NetworkCredential("user", "pass"),
            BaseUri = null,
        };

        Should.Throw<InvalidOperationException>(
            () => DynamicHttpClientConfiguration.ApplyHttpAuth(additionalHandlers, handler, config));
    }

    [Fact]
    public void Configure_DelugeType_ConfiguresCookiesRedirectAndDecompression()
    {
        HttpClientConfig config = new()
        {
            Type = HttpClientType.Deluge,
            Timeout = 30,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.GZip,
        };

        using ServiceProvider provider = CreateProvider(config);
        DynamicHttpClientConfiguration configuration = new(provider.GetRequiredService<IServiceScopeFactory>());
        HttpClientFactoryOptions options = new();

        configuration.Configure("client", options);

        HttpMessageHandlerBuilder builder = BuildHandler(provider, options);
        HttpClientHandler handler = builder.PrimaryHandler.ShouldBeOfType<HttpClientHandler>();
        handler.UseCookies.ShouldBeTrue();
        handler.AllowAutoRedirect.ShouldBeFalse();
        handler.AutomaticDecompression.ShouldBe(DecompressionMethods.GZip);
    }

    [Fact]
    public void Configure_UTorrentType_DisablesCookies()
    {
        HttpClientConfig config = new()
        {
            Type = HttpClientType.UTorrent,
            Timeout = 30,
        };

        using ServiceProvider provider = CreateProvider(config);
        DynamicHttpClientConfiguration configuration = new(provider.GetRequiredService<IServiceScopeFactory>());
        HttpClientFactoryOptions options = new();

        configuration.Configure("client", options);

        HttpMessageHandlerBuilder builder = BuildHandler(provider, options);
        HttpClientHandler handler = builder.PrimaryHandler.ShouldBeOfType<HttpClientHandler>();
        handler.UseCookies.ShouldBeFalse();
    }

    [Theory]
    [InlineData(HttpClientType.Default)]
    [InlineData(HttpClientType.WithRetry)]
    public void Configure_DefaultOrWithRetryType_UsesPlainHandlerAndAddsRetryPolicy(HttpClientType clientType)
    {
        HttpClientConfig config = new()
        {
            Type = clientType,
            Timeout = 30,
            RetryConfig = new RetryConfig { MaxRetries = 1 },
        };

        using ServiceProvider provider = CreateProvider(config);
        DynamicHttpClientConfiguration configuration = new(provider.GetRequiredService<IServiceScopeFactory>());
        HttpClientFactoryOptions options = new();

        configuration.Configure("client", options);

        HttpMessageHandlerBuilder builder = BuildHandler(provider, options);
        builder.PrimaryHandler.ShouldBeOfType<HttpClientHandler>();
        builder.AdditionalHandlers.ShouldContain(additionalHandler => additionalHandler is PolicyHttpMessageHandler);
    }

    private static ServiceProvider CreateProvider(HttpClientConfig config, string name = "client")
    {
        HttpClientConfigStore store = new();
        store.AddConfiguration(name, config);

        ServiceCollection services = new();
        services.AddHttpClient();
        services.AddSingleton<IHttpClientConfigStore>(store);
        services.AddSingleton<CertificateValidationService>();
        services.AddSingleton<ILogger<CertificateValidationService>>(NullLogger<CertificateValidationService>.Instance);

        return services.BuildServiceProvider();
    }

    private static HttpMessageHandlerBuilder BuildHandler(ServiceProvider provider, HttpClientFactoryOptions options)
    {
        HttpMessageHandlerBuilder builder = provider.GetRequiredService<HttpMessageHandlerBuilder>();

        foreach (Action<HttpMessageHandlerBuilder> action in options.HttpMessageHandlerBuilderActions)
        {
            action(builder);
        }

        return builder;
    }
}
