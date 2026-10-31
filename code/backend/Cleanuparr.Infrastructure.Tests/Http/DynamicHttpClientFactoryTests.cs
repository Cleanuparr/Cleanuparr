using System.Net;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Http.DynamicHttpClientSystem;
using Cleanuparr.Persistence.Models.Configuration.General;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Http;

/// <summary>
/// Covers the configurations the factory stores, without going through the HTTP pipeline.
/// </summary>
public sealed class DynamicHttpClientFactoryTests
{
    private readonly IHttpClientConfigStore _store = new HttpClientConfigStore();
    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();
    private readonly OptionsCache<HttpClientFactoryOptions> _optionsCache = new();
    private readonly DynamicHttpClientFactory _factory;

    public DynamicHttpClientFactoryTests()
    {
        _httpClientFactory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient());

        _factory = new DynamicHttpClientFactory(
            _httpClientFactory,
            _store,
            _optionsCache,
            NullLogger<DynamicHttpClientFactory>.Instance);
    }

    private List<string> RequestedNames()
    {
        List<string> names = [];
        _httpClientFactory.CreateClient(Arg.Do<string>(names.Add)).Returns(_ => new HttpClient());
        return names;
    }

    [Fact]
    public void CreateClient_ShouldThrow_WhenTheNameWasNeverRegistered()
    {
        Should.Throw<InvalidOperationException>(() => _factory.CreateClient("unknown"));
    }

    [Fact]
    public void RegisterDelugeClient_ShouldAllowRedirectsAndDecompression()
    {
        _factory.RegisterDelugeClient("deluge", timeout: 30, new RetryConfig { MaxRetries = 2 },
            CertificateValidationType.Enabled, sendUserAgent: true);

        HttpClientConfig config = Stored("deluge");
        config.Type.ShouldBe(HttpClientType.Deluge);
        config.AllowAutoRedirect.ShouldBeTrue();
        config.AutomaticDecompression.ShouldBe(DecompressionMethods.GZip | DecompressionMethods.Deflate);
        config.SendUserAgent.ShouldBeTrue();
    }

    [Theory]
    [InlineData(HttpClientType.Deluge)]
    [InlineData(HttpClientType.UTorrent)]
    [InlineData(HttpClientType.WithRetry)]
    public void RegisterDownloadClient_ShouldStoreTheRequestedType(HttpClientType clientType)
    {
        _factory.RegisterDownloadClient("client", timeout: 30, clientType, new RetryConfig { MaxRetries = 1 },
            CertificateValidationType.Enabled, sendUserAgent: true);

        HttpClientConfig config = Stored("client");
        config.Type.ShouldBe(clientType);
        config.Timeout.ShouldBe(30);
        config.RetryConfig!.MaxRetries.ShouldBe(1);
        config.SendUserAgent.ShouldBeTrue();
    }

    [Fact]
    public void RegisterPlainClient_ShouldStoreNoRetryPolicy()
    {
        _factory.RegisterPlainClient("plain", sendUserAgent: true);

        HttpClientConfig config = Stored("plain");
        config.Type.ShouldBe(HttpClientType.Plain);
        config.RetryConfig.ShouldBeNull();
        config.SendUserAgent.ShouldBeTrue();
    }

    [Fact]
    public void UnregisterConfiguration_ShouldDropTheStoredConfiguration()
    {
        _factory.RegisterPlainClient("plain", sendUserAgent: true);

        _factory.UnregisterConfiguration("plain");

        _store.TryGetConfiguration("plain", out _).ShouldBeFalse();
    }

    [Fact]
    public void UpdateAllClientsFromGeneralConfig_ShouldRefreshEveryStoredClient()
    {
        _factory.RegisterRetryClient("retry", timeout: 1, new RetryConfig { MaxRetries = 0 },
            CertificateValidationType.Enabled, sendUserAgent: false);
        _factory.RegisterPlainClient("plain", sendUserAgent: false);

        _factory.UpdateAllClientsFromGeneralConfig(new GeneralConfig
        {
            HttpTimeout = 55,
            HttpMaxRetries = 9,
            HttpCertificateValidation = CertificateValidationType.Disabled,
            HttpSendUserAgent = true,
        });

        HttpClientConfig retry = Stored("retry");
        retry.Timeout.ShouldBe(55);
        retry.CertificateValidationType.ShouldBe(CertificateValidationType.Disabled);
        retry.RetryConfig!.MaxRetries.ShouldBe(9);
        retry.SendUserAgent.ShouldBeTrue();

        Stored("plain").SendUserAgent.ShouldBeTrue();
    }

    [Fact]
    public void UpdateAllClientsFromGeneralConfig_ShouldDoNothing_WhenNothingIsRegistered()
    {
        Should.NotThrow(() => _factory.UpdateAllClientsFromGeneralConfig(new GeneralConfig()));

        _store.GetAllConfigurations().ShouldBeEmpty();
    }

    [Fact]
    public void CreateClient_ShouldRequestAFreshHandlerName_AfterUnregisterConfiguration()
    {
        List<string> requestedNames = RequestedNames();
        _factory.RegisterPlainClient("client", sendUserAgent: false);
        _factory.CreateClient("client");

        _factory.UnregisterConfiguration("client");
        _factory.RegisterPlainClient("client", sendUserAgent: false);
        _factory.CreateClient("client");

        requestedNames.Count.ShouldBe(2);
        requestedNames[0].ShouldNotBe(requestedNames[1]);
    }

    [Fact]
    public void CreateClient_ShouldRequestAFreshHandlerName_AfterUpdateAllClientsFromGeneralConfig()
    {
        List<string> requestedNames = RequestedNames();
        _factory.RegisterRetryClient("client", timeout: 1, new RetryConfig { MaxRetries = 0 },
            CertificateValidationType.Enabled, sendUserAgent: false);
        _factory.CreateClient("client");

        _factory.UpdateAllClientsFromGeneralConfig(new GeneralConfig
        {
            HttpTimeout = 55,
            HttpMaxRetries = 9,
            HttpCertificateValidation = CertificateValidationType.Disabled,
            HttpSendUserAgent = true,
        });
        _factory.CreateClient("client");

        requestedNames.Count.ShouldBe(2);
        requestedNames[0].ShouldNotBe(requestedNames[1]);
    }

    [Fact]
    public void UpdateAllClientsFromGeneralConfig_ShouldEvictTheBareName_OnEveryUpdate()
    {
        _factory.RegisterPlainClient("client", sendUserAgent: false);
        GeneralConfig generalConfig = new()
        {
            HttpTimeout = 55,
            HttpMaxRetries = 9,
            HttpCertificateValidation = CertificateValidationType.Disabled,
            HttpSendUserAgent = true,
        };

        // First update bumps the generation past 0, so the bare name stops matching GetGeneration's output.
        _factory.UpdateAllClientsFromGeneralConfig(generalConfig);

        _optionsCache.TryAdd("client", new HttpClientFactoryOptions());
        _factory.UpdateAllClientsFromGeneralConfig(generalConfig);

        _optionsCache.TryRemove("client").ShouldBeFalse();
    }

    private HttpClientConfig Stored(string clientName)
    {
        _store.TryGetConfiguration(clientName, out HttpClientConfig? config).ShouldBeTrue();
        return config!;
    }
}
