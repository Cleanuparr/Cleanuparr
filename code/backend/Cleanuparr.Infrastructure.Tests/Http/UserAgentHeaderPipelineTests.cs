using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Http.DynamicHttpClientSystem;
using Cleanuparr.Infrastructure.Services;
using Cleanuparr.Persistence.Models.Configuration.General;
using Cleanuparr.Shared.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Http;

/// <summary>
/// Drives the real IHttpClientFactory pipeline.
/// Covers both the handler-configured clients and the plain ones.
/// </summary>
public sealed class UserAgentHeaderPipelineTests
{
    [Fact]
    public void RetryClient_ShouldCarryTheUserAgent_WhenTheSettingIsOn()
    {
        using ServiceProvider provider = BuildProvider();
        IDynamicHttpClientFactory dynamicFactory = provider.GetRequiredService<IDynamicHttpClientFactory>();
        RegisterRetryClient(dynamicFactory, sendUserAgent: true);

        using HttpClient client = dynamicFactory.CreateClient(Constants.HttpClientWithRetryName);

        client.DefaultRequestHeaders.UserAgent.ToString().ShouldBe(AppUserAgent.Value);
    }

    [Fact]
    public void PlainClient_ShouldCarryTheUserAgent_WhenTheSettingIsOn()
    {
        using ServiceProvider provider = BuildProvider();
        IDynamicHttpClientFactory dynamicFactory = provider.GetRequiredService<IDynamicHttpClientFactory>();
        dynamicFactory.RegisterPlainClient(Constants.HttpClientOidcAuthName, sendUserAgent: true);

        using HttpClient client = dynamicFactory.CreateClient(Constants.HttpClientOidcAuthName);

        client.DefaultRequestHeaders.UserAgent.ToString().ShouldBe(AppUserAgent.Value);
    }

    [Fact]
    public void PlainClient_ShouldKeepTheDefaultTimeout()
    {
        using ServiceProvider provider = BuildProvider();
        IDynamicHttpClientFactory dynamicFactory = provider.GetRequiredService<IDynamicHttpClientFactory>();
        dynamicFactory.RegisterPlainClient(Constants.HttpClientOidcAuthName, sendUserAgent: true);

        using HttpClient client = dynamicFactory.CreateClient(Constants.HttpClientOidcAuthName);

        client.Timeout.ShouldBe(TimeSpan.FromSeconds(100));
    }

    [Fact]
    public void NoClient_ShouldCarryAUserAgent_WhenTheSettingIsOff()
    {
        using ServiceProvider provider = BuildProvider();
        IDynamicHttpClientFactory dynamicFactory = provider.GetRequiredService<IDynamicHttpClientFactory>();
        RegisterRetryClient(dynamicFactory, sendUserAgent: false);
        dynamicFactory.RegisterPlainClient(Constants.HttpClientOidcAuthName, sendUserAgent: false);

        using HttpClient retry = dynamicFactory.CreateClient(Constants.HttpClientWithRetryName);
        using HttpClient plain = dynamicFactory.CreateClient(Constants.HttpClientOidcAuthName);

        retry.DefaultRequestHeaders.UserAgent.ShouldBeEmpty();
        plain.DefaultRequestHeaders.UserAgent.ShouldBeEmpty();
    }

    [Fact]
    public void PlainClient_ShouldPickUpTheSetting_WithoutARestart()
    {
        using ServiceProvider provider = BuildProvider();
        IDynamicHttpClientFactory dynamicFactory = provider.GetRequiredService<IDynamicHttpClientFactory>();
        dynamicFactory.RegisterPlainClient(Constants.HttpClientOidcAuthName, sendUserAgent: false);

        using HttpClient before = dynamicFactory.CreateClient(Constants.HttpClientOidcAuthName);
        before.DefaultRequestHeaders.UserAgent.ShouldBeEmpty();

        dynamicFactory.UpdateAllClientsFromGeneralConfig(new GeneralConfig { HttpSendUserAgent = true });

        using HttpClient after = dynamicFactory.CreateClient(Constants.HttpClientOidcAuthName);

        after.DefaultRequestHeaders.UserAgent.ToString().ShouldBe(AppUserAgent.Value);

        // The setting reaches the next client, never one already handed out.
        // Every consumer is scoped or disposes per call, so none outlives a change.
        before.DefaultRequestHeaders.UserAgent.ShouldBeEmpty();
    }

    private static void RegisterRetryClient(IDynamicHttpClientFactory dynamicFactory, bool sendUserAgent) =>
        dynamicFactory.RegisterRetryClient(
            Constants.HttpClientWithRetryName,
            timeout: 100,
            new RetryConfig { MaxRetries = 0 },
            CertificateValidationType.Enabled,
            sendUserAgent);

    private static ServiceProvider BuildProvider()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<CertificateValidationService>();
        services.AddDynamicHttpClients();

        return services.BuildServiceProvider();
    }
}
