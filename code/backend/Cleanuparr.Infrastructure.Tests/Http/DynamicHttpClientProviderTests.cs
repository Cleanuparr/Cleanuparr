using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Http;
using Cleanuparr.Infrastructure.Http.DynamicHttpClientSystem;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.General;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Http;

/// <summary>
/// Covers the second registration site for the general HTTP settings.
/// </summary>
public sealed class DynamicHttpClientProviderTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<DataContext> _options;
    private readonly IDynamicHttpClientFactory _clientFactory = Substitute.For<IDynamicHttpClientFactory>();

    public DynamicHttpClientProviderTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<DataContext>()
            .UseSqlite(_connection)
            .Options;

        using DataContext context = new(_options);
        context.Database.EnsureCreated();

        _clientFactory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient());
    }

    [Theory]
    [InlineData(DownloadClientTypeName.Deluge, HttpClientType.Deluge)]
    [InlineData(DownloadClientTypeName.uTorrent, HttpClientType.UTorrent)]
    [InlineData(DownloadClientTypeName.qBittorrent, HttpClientType.WithRetry)]
    [InlineData(DownloadClientTypeName.Transmission, HttpClientType.WithRetry)]
    [InlineData(DownloadClientTypeName.rTorrent, HttpClientType.WithRetry)]
    public void CreateClient_ShouldMapTheDownloadClientType(DownloadClientTypeName typeName, HttpClientType expected)
    {
        Seed(sendUserAgent: true);
        DownloadClientConfig config = BuildConfig(typeName);

        using HttpClient client = BuildProvider().CreateClient(config);

        _clientFactory.Received(1).RegisterDownloadClient(
            $"DownloadClient_{config.Id}",
            Arg.Any<int>(),
            expected,
            Arg.Any<RetryConfig>(),
            Arg.Any<CertificateValidationType>(),
            Arg.Any<bool>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CreateClient_ShouldPassTheUserAgentSetting(bool sendUserAgent)
    {
        Seed(sendUserAgent: sendUserAgent);

        using HttpClient client = BuildProvider().CreateClient(BuildConfig(DownloadClientTypeName.qBittorrent));

        _clientFactory.Received(1).RegisterDownloadClient(
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<HttpClientType>(),
            Arg.Any<RetryConfig>(),
            Arg.Any<CertificateValidationType>(),
            sendUserAgent);
    }

    [Fact]
    public void CreateClient_ShouldCarryTheStoredHttpSettings()
    {
        Seed(sendUserAgent: false, timeout: 33, maxRetries: 4, certificate: CertificateValidationType.DisabledForLocalAddresses);

        using HttpClient client = BuildProvider().CreateClient(BuildConfig(DownloadClientTypeName.qBittorrent));

        _clientFactory.Received(1).RegisterDownloadClient(
            Arg.Any<string>(),
            33,
            Arg.Any<HttpClientType>(),
            Arg.Is<RetryConfig>(retry => retry.MaxRetries == 4 && retry.ExcludeUnauthorized),
            CertificateValidationType.DisabledForLocalAddresses,
            false);
    }

    [Fact]
    public void CreateClient_ShouldSetTheBaseAddress()
    {
        Seed(sendUserAgent: false);

        using HttpClient client = BuildProvider().CreateClient(BuildConfig(DownloadClientTypeName.qBittorrent));

        client.BaseAddress.ShouldNotBeNull();
        client.BaseAddress!.ToString().ShouldStartWith("http://localhost:8080/");
    }

    private DynamicHttpClientProvider BuildProvider()
    {
        ServiceCollection services = new();
        services.AddScoped(_ => new DataContext(_options));

        return new DynamicHttpClientProvider(
            NullLogger<DynamicHttpClientProvider>.Instance,
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            _clientFactory);
    }

    private static DownloadClientConfig BuildConfig(DownloadClientTypeName typeName) => new()
    {
        Id = Guid.NewGuid(),
        Name = $"test-{typeName}",
        TypeName = typeName,
        Type = DownloadClientType.Torrent,
        Enabled = true,
        Host = new Uri("http://localhost:8080"),
    };

    private void Seed(
        bool sendUserAgent,
        ushort timeout = 100,
        ushort maxRetries = 0,
        CertificateValidationType certificate = CertificateValidationType.Enabled)
    {
        using DataContext context = new(_options);

        context.GeneralConfigs.RemoveRange(context.GeneralConfigs);
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
