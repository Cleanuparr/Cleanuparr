using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient.Deluge;
using Cleanuparr.Infrastructure.Http.DynamicHttpClientSystem;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.General;
using Cleanuparr.Shared.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Http;

/// <summary>
/// Covers the startup registrations that put every client in reach of the general HTTP settings.
/// </summary>
public sealed class HttpClientConfigurationServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<DataContext> _options;
    private readonly IDynamicHttpClientFactory _clientFactory = Substitute.For<IDynamicHttpClientFactory>();

    public HttpClientConfigurationServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<DataContext>()
            .UseSqlite(_connection)
            .Options;

        using DataContext context = new(_options);
        context.Database.EnsureCreated();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StartAsync_ShouldPassTheUserAgentSetting_ToEveryRegistration(bool sendUserAgent)
    {
        Seed(sendUserAgent: sendUserAgent);

        await BuildService().StartAsync(CancellationToken.None);

        _clientFactory.Received(1).RegisterRetryClient(
            Constants.HttpClientWithRetryName,
            Arg.Any<int>(),
            Arg.Any<RetryConfig>(),
            Arg.Any<CertificateValidationType>(),
            sendUserAgent);

        _clientFactory.Received(1).RegisterDelugeClient(
            nameof(DelugeService),
            Arg.Any<int>(),
            Arg.Any<RetryConfig>(),
            Arg.Any<CertificateValidationType>(),
            sendUserAgent);

        _clientFactory.Received(1).RegisterPlainClient(Constants.HttpClientPlexAuthName, sendUserAgent);
        _clientFactory.Received(1).RegisterPlainClient(Constants.HttpClientOidcAuthName, sendUserAgent);
        _clientFactory.Received(1).RegisterPlainClient(Constants.HttpClientConnectivityName, sendUserAgent);
    }

    [Fact]
    public async Task StartAsync_ShouldCarryTheStoredHttpSettings_IntoTheRetryClient()
    {
        Seed(sendUserAgent: true, timeout: 42, maxRetries: 7, certificate: CertificateValidationType.Disabled);

        await BuildService().StartAsync(CancellationToken.None);

        _clientFactory.Received(1).RegisterRetryClient(
            Constants.HttpClientWithRetryName,
            42,
            Arg.Is<RetryConfig>(retry => retry.MaxRetries == 7 && retry.ExcludeUnauthorized),
            CertificateValidationType.Disabled,
            true);
    }

    [Fact]
    public async Task StartAsync_ShouldThrow_WhenNoGeneralConfigExists()
    {
        await Should.ThrowAsync<InvalidOperationException>(
            () => BuildService().StartAsync(CancellationToken.None));
    }

    private HttpClientConfigurationService BuildService()
    {
        ServiceCollection services = new();
        services.AddScoped(_ => new DataContext(_options));

        return new HttpClientConfigurationService(
            _clientFactory,
            NullLogger<HttpClientConfigurationService>.Instance,
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());
    }

    private void Seed(
        bool sendUserAgent,
        ushort timeout = 100,
        ushort maxRetries = 0,
        CertificateValidationType certificate = CertificateValidationType.Enabled)
    {
        using DataContext context = new(_options);

        context.GeneralConfigs.Add(new GeneralConfig
        {
            Id = Guid.NewGuid(),
            HttpSendUserAgent = sendUserAgent,
            HttpTimeout = timeout,
            HttpMaxRetries = maxRetries,
            HttpCertificateValidation = certificate,
            IgnoredDownloads = [],
            Log = new LoggingConfig()
        });
        context.SaveChanges();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
