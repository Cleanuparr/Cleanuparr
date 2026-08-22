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
    public void RetryClient_ShouldCarryNoUserAgent_WhenTheSettingIsOff()
    {
        using ServiceProvider provider = BuildProvider();
        IDynamicHttpClientFactory dynamicFactory = provider.GetRequiredService<IDynamicHttpClientFactory>();
        RegisterRetryClient(dynamicFactory, sendUserAgent: false);

        using HttpClient client = dynamicFactory.CreateClient(Constants.HttpClientWithRetryName);

        client.DefaultRequestHeaders.UserAgent.ShouldBeEmpty();
    }

    [Fact]
    public void RetryClient_ShouldPickUpTheSetting_WithoutARestart()
    {
        using ServiceProvider provider = BuildProvider();
        IDynamicHttpClientFactory dynamicFactory = provider.GetRequiredService<IDynamicHttpClientFactory>();
        RegisterRetryClient(dynamicFactory, sendUserAgent: false);

        using HttpClient before = dynamicFactory.CreateClient(Constants.HttpClientWithRetryName);
        before.DefaultRequestHeaders.UserAgent.ShouldBeEmpty();

        dynamicFactory.UpdateAllClientsFromGeneralConfig(new GeneralConfig { HttpSendUserAgent = true });

        using HttpClient after = dynamicFactory.CreateClient(Constants.HttpClientWithRetryName);

        after.DefaultRequestHeaders.UserAgent.ToString().ShouldBe(AppUserAgent.Value);
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
