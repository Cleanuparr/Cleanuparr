using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.BlacklistSync;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.DownloadClient.QBittorrent;
using Cleanuparr.Infrastructure.Helpers;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.BlacklistSync;
using Cleanuparr.Persistence.Models.State;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.BlacklistSync;

public class BlacklistSynchronizerTests : IDisposable
{
    private readonly Mock<ILogger<BlacklistSynchronizer>> _loggerMock;
    private readonly DataContext _dataContext;
    private readonly Mock<IDownloadServiceFactory> _downloadServiceFactoryMock;
    private readonly Mock<IDryRunInterceptor> _dryRunInterceptorMock;
    private readonly FileReader _fileReader;
    private readonly BlacklistSynchronizer _synchronizer;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly SqliteConnection _connection;

    public BlacklistSynchronizerTests()
    {
        _loggerMock = new Mock<ILogger<BlacklistSynchronizer>>();

        // Use SQLite in-memory with shared connection to support complex types
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<DataContext>()
            .UseSqlite(_connection)
            .Options;

        _dataContext = new DataContext(options);
        _dataContext.Database.EnsureCreated();

        _downloadServiceFactoryMock = new Mock<IDownloadServiceFactory>();

        _dryRunInterceptorMock = new Mock<IDryRunInterceptor>();
        // Setup interceptor to execute the action with params using DynamicInvoke
        _dryRunInterceptorMock
            .Setup(d => d.InterceptAsync(It.IsAny<Delegate>(), It.IsAny<object[]>()))
            .Returns((Delegate action, object[] parameters) =>
            {
                var result = action.DynamicInvoke(parameters);
                if (result is Task task)
                {
                    return task;
                }
                return Task.CompletedTask;
            });

        // Setup mock HTTP handler for FileReader
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(_httpMessageHandlerMock.Object);

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        _fileReader = new FileReader(httpClientFactoryMock.Object);

        _synchronizer = new BlacklistSynchronizer(
            _loggerMock.Object,
            _dataContext,
            _downloadServiceFactoryMock.Object,
            _fileReader,
            _dryRunInterceptorMock.Object
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
        _downloadServiceFactoryMock.Verify(
            f => f.GetDownloadService(It.IsAny<DownloadClientConfig>()),
            Times.Never);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("disabled")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
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
        _downloadServiceFactoryMock.Verify(
            f => f.GetDownloadService(It.IsAny<DownloadClientConfig>()),
            Times.Never);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("path is not configured")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPathIsWhitespace_LogsWarningAndReturns()
    {
        // Arrange
        await SetupBlacklistSyncConfig(enabled: true, blacklistPath: "   ");

        // Act
        await _synchronizer.ExecuteAsync();

        // Assert
        _downloadServiceFactoryMock.Verify(
            f => f.GetDownloadService(It.IsAny<DownloadClientConfig>()),
            Times.Never);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("path is not configured")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
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
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No enabled qBittorrent clients")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
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
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No enabled qBittorrent clients")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
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
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No enabled qBittorrent clients")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
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
        _downloadServiceFactoryMock.Verify(
            f => f.GetDownloadService(It.IsAny<DownloadClientConfig>()),
            Times.Never);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("already synced")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
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
        _dryRunInterceptorMock.Verify(
            d => d.InterceptAsync(It.IsAny<Delegate>(), It.IsAny<object[]>()),
            Times.AtLeastOnce);
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
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(content)
            });
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
