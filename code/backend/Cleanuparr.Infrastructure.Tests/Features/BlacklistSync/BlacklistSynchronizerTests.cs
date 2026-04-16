using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.BlacklistSync;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.DownloadClient.QBittorrent;
using Cleanuparr.Infrastructure.Helpers;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.BlacklistSync;
using Cleanuparr.Persistence.Models.State;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Net;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.BlacklistSync;

public class BlacklistSynchronizerTests : IDisposable
{
    private readonly ILogger<BlacklistSynchronizer> _logger;
    private readonly DataContext _dataContext;
    private readonly IDownloadServiceFactory _downloadServiceFactory;
    private readonly IDryRunInterceptor _dryRunInterceptor;
    private readonly FileReader _fileReader;
    private readonly BlacklistSynchronizer _synchronizer;
    private readonly FakeHttpMessageHandler _httpMessageHandler;
    private readonly SqliteConnection _connection;

    public BlacklistSynchronizerTests()
    {
        _logger = Substitute.For<ILogger<BlacklistSynchronizer>>();

        // Use SQLite in-memory with shared connection to support complex types
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<DataContext>()
            .UseSqlite(_connection)
            .Options;

        _dataContext = new DataContext(options);
        _dataContext.Database.EnsureCreated();

        _downloadServiceFactory = Substitute.For<IDownloadServiceFactory>();

        _dryRunInterceptor = Substitute.For<IDryRunInterceptor>();
        // Setup interceptor to execute the action with params using DynamicInvoke
        _dryRunInterceptor.InterceptAsync(default!, default!)
            .ReturnsForAnyArgs(ci =>
            {
                var action = ci.ArgAt<Delegate>(0);
                var parameters = ci.ArgAt<object[]>(1);
                var result = action.DynamicInvoke(parameters);
                if (result is Task task)
                {
                    return task;
                }
                return Task.CompletedTask;
            });

        // Setup FakeHttpMessageHandler for FileReader
        _httpMessageHandler = new FakeHttpMessageHandler();
        var httpClient = new HttpClient(_httpMessageHandler);

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        _fileReader = new FileReader(httpClientFactory);

        _synchronizer = new BlacklistSynchronizer(
            _logger,
            _dataContext,
            _downloadServiceFactory,
            _fileReader,
            _dryRunInterceptor
        );
    }

    public void Dispose()
    {
        _dataContext.Dispose();
        _connection.Dispose();
    }

    #region ExecuteAsync - Disabled Tests

    [Fact]
    public async Task ExecuteAsync_WhenDisabled_ReturnsEarlyWithoutProcessing()
    {
        // Arrange
        await SetupBlacklistSyncConfig(enabled: false);

        // Act
        await _synchronizer.ExecuteAsync();

        // Assert
        _downloadServiceFactory.DidNotReceive().GetDownloadService(Arg.Any<DownloadClientConfig>());

        _logger.ReceivedLogContaining(LogLevel.Debug, "disabled");
    }

    #endregion

    #region ExecuteAsync - Path Not Configured Tests

    [Fact]
    public async Task ExecuteAsync_WhenPathNotConfigured_LogsWarningAndReturns()
    {
        // Arrange
        await SetupBlacklistSyncConfig(enabled: true, blacklistPath: null);

        // Act
        await _synchronizer.ExecuteAsync();

        // Assert
        _downloadServiceFactory.DidNotReceive().GetDownloadService(Arg.Any<DownloadClientConfig>());

        _logger.ReceivedLogContaining(LogLevel.Warning, "path is not configured");
    }

    [Fact]
    public async Task ExecuteAsync_WhenPathIsWhitespace_LogsWarningAndReturns()
    {
        // Arrange
        await SetupBlacklistSyncConfig(enabled: true, blacklistPath: "   ");

        // Act
        await _synchronizer.ExecuteAsync();

        // Assert
        _downloadServiceFactory.DidNotReceive().GetDownloadService(Arg.Any<DownloadClientConfig>());

        _logger.ReceivedLogContaining(LogLevel.Warning, "path is not configured");
    }

    #endregion

    #region ExecuteAsync - No Clients Tests

    [Fact]
    public async Task ExecuteAsync_WhenNoQBittorrentClients_LogsDebugAndReturns()
    {
        // Arrange
        await SetupBlacklistSyncConfig(enabled: true, blacklistPath: "https://example.com/blocklist.txt");
        SetupHttpResponse("pattern1\npattern2");

        // Don't add any download clients

        // Act
        await _synchronizer.ExecuteAsync();

        // Assert
        _logger.ReceivedLogContaining(LogLevel.Debug, "No enabled qBittorrent clients");
    }

    [Fact]
    public async Task ExecuteAsync_WhenOnlyDelugeClients_LogsDebugAndReturns()
    {
        // Arrange
        await SetupBlacklistSyncConfig(enabled: true, blacklistPath: "https://example.com/blocklist.txt");
        SetupHttpResponse("pattern1\npattern2");

        // Add only a Deluge client
        await AddDownloadClient(DownloadClientTypeName.Deluge, enabled: true);

        // Act
        await _synchronizer.ExecuteAsync();

        // Assert
        _logger.ReceivedLogContaining(LogLevel.Debug, "No enabled qBittorrent clients");
    }

    [Fact]
    public async Task ExecuteAsync_WhenDisabledQBittorrentClient_DoesNotProcess()
    {
        // Arrange
        await SetupBlacklistSyncConfig(enabled: true, blacklistPath: "https://example.com/blocklist.txt");
        SetupHttpResponse("pattern1\npattern2");

        // Add a disabled qBittorrent client
        await AddDownloadClient(DownloadClientTypeName.qBittorrent, enabled: false);

        // Act
        await _synchronizer.ExecuteAsync();

        // Assert
        _logger.ReceivedLogContaining(LogLevel.Debug, "No enabled qBittorrent clients");
    }

    #endregion

    #region ExecuteAsync - Already Synced Tests

    [Fact]
    public async Task ExecuteAsync_WhenClientAlreadySynced_SkipsClient()
    {
        // Arrange
        var patterns = "pattern1\npattern2";
        await SetupBlacklistSyncConfig(enabled: true, blacklistPath: "https://example.com/blocklist.txt");
        SetupHttpResponse(patterns);

        var clientId = await AddDownloadClient(DownloadClientTypeName.qBittorrent, enabled: true);

        // Calculate the expected hash (same as ComputeHash in BlacklistSynchronizer)
        var cleanPatterns = string.Join('\n', patterns.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !string.IsNullOrWhiteSpace(p)));
        var hash = ComputeHash(cleanPatterns);

        // Add sync history for this client with the same hash
        _dataContext.BlacklistSyncHistory.Add(new BlacklistSyncHistory
        {
            Hash = hash,
            DownloadClientId = clientId
        });
        await _dataContext.SaveChangesAsync();

        // Act
        await _synchronizer.ExecuteAsync();

        // Assert
        _downloadServiceFactory.DidNotReceive().GetDownloadService(Arg.Any<DownloadClientConfig>());

        _logger.ReceivedLogContaining(LogLevel.Debug, "already synced");
    }

    #endregion

    #region ExecuteAsync - Dry Run Tests

    [Fact]
    public async Task ExecuteAsync_UsesDryRunInterceptor()
    {
        // Arrange
        await SetupBlacklistSyncConfig(enabled: true, blacklistPath: "https://example.com/blocklist.txt");
        SetupHttpResponse("pattern1\npattern2");

        // Act
        await _synchronizer.ExecuteAsync();

        // Assert - Verify interceptor was called (with Delegate, not Func<object, object, Task>)
        await _dryRunInterceptor.Received()
            .InterceptAsync(Arg.Any<Delegate>(), Arg.Any<object[]>());
    }

    #endregion

    #region Helper Methods

    private async Task SetupBlacklistSyncConfig(bool enabled, string? blacklistPath = null)
    {
        var config = new BlacklistSyncConfig
        {
            Enabled = enabled,
            BlacklistPath = blacklistPath
        };

        _dataContext.BlacklistSyncConfigs.Add(config);
        await _dataContext.SaveChangesAsync();
    }

    private async Task<Guid> AddDownloadClient(DownloadClientTypeName typeName, bool enabled)
    {
        var client = new DownloadClientConfig
        {
            Id = Guid.NewGuid(),
            Name = $"Test {typeName} Client",
            TypeName = typeName,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://test.example.com"),
            Enabled = enabled
        };

        _dataContext.DownloadClients.Add(client);
        await _dataContext.SaveChangesAsync();

        return client.Id;
    }

    private void SetupHttpResponse(string content)
    {
        _httpMessageHandler.SetupResponse((req, ct) => Task.FromResult(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(content)
        }));
    }

    private static string ComputeHash(string content)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(content);
        byte[] hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    #endregion
}
