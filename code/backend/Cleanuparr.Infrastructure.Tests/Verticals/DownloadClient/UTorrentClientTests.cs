using System.Net;
using Cleanuparr.Domain.Entities.UTorrent.Response;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;
using Cleanuparr.Persistence.Models.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Verticals.DownloadClient;

public class UTorrentClientTests
{
    private readonly UTorrentClient _client;
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly DownloadClientConfig _config;
    private readonly Mock<IUTorrentAuthenticator> _mockAuthenticator;
    private readonly Mock<IUTorrentHttpService> _mockHttpService;
    private readonly Mock<IUTorrentResponseParser> _mockResponseParser;
    private readonly Mock<ILogger<UTorrentClient>> _mockLogger;

    public UTorrentClientTests()
    {
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _mockAuthenticator = new Mock<IUTorrentAuthenticator>();
        _mockHttpService = new Mock<IUTorrentHttpService>();
        _mockResponseParser = new Mock<IUTorrentResponseParser>();
        _mockLogger = new Mock<ILogger<UTorrentClient>>();
        
        _config = new DownloadClientConfig
        {
            Name = "test",
            Type = DownloadClientType.Torrent,
            TypeName = DownloadClientTypeName.uTorrent,
            Host = new Uri("http://localhost:8080"),
            Username = "admin",
            Password = "password"
        };

        _client = new UTorrentClient(
            _config,
            _mockAuthenticator.Object,
            _mockHttpService.Object,
            _mockResponseParser.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task GetTorrentFilesAsync_ShouldDeserializeMixedArrayCorrectly()
    {
        // Arrange
        var mockResponse = new UTorrentResponse<object>
        {
            Build = 30470,
            FilesDto = new object[]
            {
                "F0616FB199B78254474AF6D72705177E71D713ED", // Hash (string)
                new object[] // File 1
                {
                    "test name",
                    2604L,
                    0L,
                    2,
                    0,
                    1,
                    false,
                    -1,
                    -1,
                    -1,
                    -1,
                    -1,
                    0
                },
                new object[] // File 2
                {
                    "Dir1/Dir11/test11.zipx",
                    2604L,
                    0L,
                    2,
                    0,
                    1,
                    false,
                    -1,
                    -1,
                    -1,
                    -1,
                    -1,
                    0
                },
                new object[] // File 3
                {
                    "Dir1/sample.txt",
                    2604L,
                    0L,
                    2,
                    0,
                    1,
                    false,
                    -1,
                    -1,
                    -1,
                    -1,
                    -1,
                    0
                }
            }
        };

        // Mock the token request
        var tokenResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("<div id='token'>test-token</div>")
        };
        tokenResponse.Headers.Add("Set-Cookie", "GUID=test-guid; path=/");

        // Mock the files request
        var filesResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(JsonConvert.SerializeObject(mockResponse))
        };

        // Setup mock to return different responses based on URL
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", 
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.AbsolutePath.Contains("token.html")), 
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(tokenResponse);

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", 
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.AbsolutePath.Contains("gui") && req.RequestUri.Query.Contains("action=getfiles")), 
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(filesResponse);

        // Act
        var files = await _client.GetTorrentFilesAsync("test-hash");

        // Assert
        Assert.NotNull(files);
        Assert.Equal(3, files.Count);
        
        Assert.Equal("test name", files[0].Name);
        Assert.Equal(2604L, files[0].Size);
        Assert.Equal(0L, files[0].Downloaded);
        Assert.Equal(2, files[0].Priority);
        Assert.Equal(0, files[0].Index);
        
        Assert.Equal("Dir1/Dir11/test11.zipx", files[1].Name);
        Assert.Equal(2604L, files[1].Size);
        Assert.Equal(0L, files[1].Downloaded);
        Assert.Equal(2, files[1].Priority);
        Assert.Equal(1, files[1].Index);
        
        Assert.Equal("Dir1/sample.txt", files[2].Name);
        Assert.Equal(2604L, files[2].Size);
        Assert.Equal(0L, files[2].Downloaded);
        Assert.Equal(2, files[2].Priority);
        Assert.Equal(2, files[2].Index);
    }

    [Fact]
    public async Task GetTorrentFilesAsync_ShouldHandleEmptyResponse()
    {
        // Arrange
        var mockResponse = new UTorrentResponse<object>
        {
            Build = 30470,
            FilesDto = new object[]
            {
                "F0616FB199B78254474AF6D72705177E71D713ED" // Only hash, no files
            }
        };

        // Mock the token request
        var tokenResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("<div id='token'>test-token</div>")
        };
        tokenResponse.Headers.Add("Set-Cookie", "GUID=test-guid; path=/");

        // Mock the files request
        var filesResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(JsonConvert.SerializeObject(mockResponse))
        };

        // Setup mock to return different responses based on URL
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", 
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.AbsolutePath.Contains("token.html")), 
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(tokenResponse);

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", 
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.AbsolutePath.Contains("gui") && req.RequestUri.Query.Contains("action=getfiles")), 
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(filesResponse);

        // Act
        var files = await _client.GetTorrentFilesAsync("test-hash");

        // Assert
        Assert.NotNull(files);
        Assert.Empty(files);
    }

    [Fact]
    public async Task GetTorrentFilesAsync_ShouldHandleNullResponse()
    {
        // Arrange
        var mockResponse = new UTorrentResponse<object>
        {
            Build = 30470,
            FilesDto = null
        };

        // Mock the token request
        var tokenResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("<div id='token'>test-token</div>")
        };
        tokenResponse.Headers.Add("Set-Cookie", "GUID=test-guid; path=/");

        // Mock the files request
        var filesResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(JsonConvert.SerializeObject(mockResponse))
        };

        // Setup mock to return different responses based on URL
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", 
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.AbsolutePath.Contains("token.html")), 
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(tokenResponse);

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", 
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.AbsolutePath.Contains("gui") && req.RequestUri.Query.Contains("action=getfiles")), 
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(filesResponse);

        // Act
        var files = await _client.GetTorrentFilesAsync("test-hash");

        // Assert
        Assert.NotNull(files);
        Assert.Empty(files);
    }
} 