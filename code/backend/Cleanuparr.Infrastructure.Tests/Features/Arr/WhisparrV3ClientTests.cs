using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Infrastructure.Features.Arr;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Interceptors;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Arr;

public class WhisparrV3ClientTests
{
    private readonly Mock<ILogger<WhisparrV3Client>> _loggerMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<IStriker> _strikerMock;
    private readonly Mock<IDryRunInterceptor> _dryRunInterceptorMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly WhisparrV3Client _client;

    public WhisparrV3ClientTests()
    {
        _loggerMock = new Mock<ILogger<WhisparrV3Client>>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _strikerMock = new Mock<IStriker>();
        _dryRunInterceptorMock = new Mock<IDryRunInterceptor>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        _client = new WhisparrV3Client(
            _loggerMock.Object,
            _httpClientFactoryMock.Object,
            _strikerMock.Object,
            _dryRunInterceptorMock.Object
        );
    }

    #region IsRecordValid Tests

    [Fact]
    public void IsRecordValid_WhenMovieIdIsZero_ReturnsFalse()
    {
        // Arrange
        var record = new QueueRecord
        {
            Id = 1,
            Title = "Test Movie",
            DownloadId = "abc123",
            Protocol = "torrent",
            MovieId = 0
        };

        // Act
        var result = _client.IsRecordValid(record);

        // Assert
        Assert.False(result);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("movie id missing")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    [Fact]
    public void IsRecordValid_WhenMovieIdIsSet_ReturnsTrue()
    {
        // Arrange
        var record = new QueueRecord
        {
            Id = 1,
            Title = "Test Movie",
            DownloadId = "abc123",
            Protocol = "torrent",
            MovieId = 42
        };

        // Act
        var result = _client.IsRecordValid(record);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsRecordValid_WhenDownloadIdIsNull_ReturnsFalse()
    {
        // Arrange
        var record = new QueueRecord
        {
            Id = 1,
            Title = "Test Movie",
            DownloadId = null!,
            Protocol = "torrent",
            MovieId = 42
        };

        // Act
        var result = _client.IsRecordValid(record);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsRecordValid_WhenDownloadIdIsEmpty_ReturnsFalse()
    {
        // Arrange
        var record = new QueueRecord
        {
            Id = 1,
            Title = "Test Movie",
            DownloadId = "",
            Protocol = "torrent",
            MovieId = 42
        };

        // Act
        var result = _client.IsRecordValid(record);

        // Assert
        Assert.False(result);
    }

    #endregion
}
