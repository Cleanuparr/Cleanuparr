using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Infrastructure.Features.Arr;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Interceptors;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Arr;

public class WhisparrV2ClientTests
{
    private readonly Mock<ILogger<WhisparrV2Client>> _loggerMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<IStriker> _strikerMock;
    private readonly Mock<IDryRunInterceptor> _dryRunInterceptorMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly WhisparrV2Client _client;

    public WhisparrV2ClientTests()
    {
        _loggerMock = new Mock<ILogger<WhisparrV2Client>>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _strikerMock = new Mock<IStriker>();
        _dryRunInterceptorMock = new Mock<IDryRunInterceptor>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        _client = new WhisparrV2Client(
            _loggerMock.Object,
            _httpClientFactoryMock.Object,
            _strikerMock.Object,
            _dryRunInterceptorMock.Object
        );
    }

    #region IsRecordValid Tests

    [Fact]
    public void IsRecordValid_WhenEpisodeIdIsZero_ReturnsFalse()
    {
        // Arrange
        var record = new QueueRecord
        {
            Id = 1,
            Title = "Test Episode",
            DownloadId = "abc123",
            Protocol = "torrent",
            EpisodeId = 0,
            SeriesId = 1
        };

        // Act
        var result = _client.IsRecordValid(record);

        // Assert
        Assert.False(result);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("episode id and/or series id missing")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    [Fact]
    public void IsRecordValid_WhenSeriesIdIsZero_ReturnsFalse()
    {
        // Arrange
        var record = new QueueRecord
        {
            Id = 1,
            Title = "Test Episode",
            DownloadId = "abc123",
            Protocol = "torrent",
            EpisodeId = 1,
            SeriesId = 0
        };

        // Act
        var result = _client.IsRecordValid(record);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsRecordValid_WhenBothIdsAreZero_ReturnsFalse()
    {
        // Arrange
        var record = new QueueRecord
        {
            Id = 1,
            Title = "Test Episode",
            DownloadId = "abc123",
            Protocol = "torrent",
            EpisodeId = 0,
            SeriesId = 0
        };

        // Act
        var result = _client.IsRecordValid(record);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsRecordValid_WhenBothIdsAreSet_ReturnsTrue()
    {
        // Arrange
        var record = new QueueRecord
        {
            Id = 1,
            Title = "Test Episode",
            DownloadId = "abc123",
            Protocol = "torrent",
            EpisodeId = 42,
            SeriesId = 10
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
            Title = "Test Episode",
            DownloadId = null!,
            Protocol = "torrent",
            EpisodeId = 42,
            SeriesId = 10
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
            Title = "Test Episode",
            DownloadId = "",
            Protocol = "torrent",
            EpisodeId = 42,
            SeriesId = 10
        };

        // Act
        var result = _client.IsRecordValid(record);

        // Assert
        Assert.False(result);
    }

    #endregion
}
