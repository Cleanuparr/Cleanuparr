using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadHunter.Consumers;
using Cleanuparr.Infrastructure.Features.DownloadHunter.Interfaces;
using Cleanuparr.Infrastructure.Features.DownloadHunter.Models;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Data.Models.Arr;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadHunter.Consumers;

public class DownloadHunterConsumerTests
{
    private readonly Mock<ILogger<DownloadHunterConsumer<SearchItem>>> _loggerMock;
    private readonly Mock<IDownloadHunter> _downloadHunterMock;
    private readonly DownloadHunterConsumer<SearchItem> _consumer;

    public DownloadHunterConsumerTests()
    {
        _loggerMock = new Mock<ILogger<DownloadHunterConsumer<SearchItem>>>();
        _downloadHunterMock = new Mock<IDownloadHunter>();
        _consumer = new DownloadHunterConsumer<SearchItem>(_loggerMock.Object, _downloadHunterMock.Object);
    }

    #region Consume Tests

    [Fact]
    public async Task Consume_CallsHuntDownloadsAsync()
    {
        // Arrange
        var request = CreateHuntRequest();
        var contextMock = CreateConsumeContextMock(request);

        _downloadHunterMock
            .Setup(h => h.HuntDownloadsAsync(It.IsAny<DownloadHuntRequest<SearchItem>>()))
            .Returns(Task.CompletedTask);

        // Act
        await _consumer.Consume(contextMock.Object);

        // Assert
        _downloadHunterMock.Verify(h => h.HuntDownloadsAsync(request), Times.Once);
    }

    [Fact]
    public async Task Consume_WhenHunterThrows_LogsErrorAndDoesNotRethrow()
    {
        // Arrange
        var request = CreateHuntRequest();
        var contextMock = CreateConsumeContextMock(request);

        _downloadHunterMock
            .Setup(h => h.HuntDownloadsAsync(It.IsAny<DownloadHuntRequest<SearchItem>>()))
            .ThrowsAsync(new Exception("Hunt failed"));

        // Act - Should not throw
        await _consumer.Consume(contextMock.Object);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("failed to search for replacement")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_PassesCorrectRequestToHunter()
    {
        // Arrange
        var request = CreateHuntRequest();
        var contextMock = CreateConsumeContextMock(request);
        DownloadHuntRequest<SearchItem>? capturedRequest = null;

        _downloadHunterMock
            .Setup(h => h.HuntDownloadsAsync(It.IsAny<DownloadHuntRequest<SearchItem>>()))
            .Callback<DownloadHuntRequest<SearchItem>>(r => capturedRequest = r)
            .Returns(Task.CompletedTask);

        // Act
        await _consumer.Consume(contextMock.Object);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal(request.InstanceType, capturedRequest.InstanceType);
        Assert.Equal(request.SearchItem.Id, capturedRequest.SearchItem.Id);
    }

    [Fact]
    public async Task Consume_WithDifferentInstanceTypes_HandlesCorrectly()
    {
        // Arrange
        var request = new DownloadHuntRequest<SearchItem>
        {
            InstanceType = InstanceType.Lidarr,
            Instance = CreateArrInstance(),
            SearchItem = new SearchItem { Id = 999 },
            Record = CreateQueueRecord(),
            JobRunId = Guid.NewGuid()
        };
        var contextMock = CreateConsumeContextMock(request);

        _downloadHunterMock
            .Setup(h => h.HuntDownloadsAsync(It.IsAny<DownloadHuntRequest<SearchItem>>()))
            .Returns(Task.CompletedTask);

        // Act
        await _consumer.Consume(contextMock.Object);

        // Assert
        _downloadHunterMock.Verify(h => h.HuntDownloadsAsync(
            It.Is<DownloadHuntRequest<SearchItem>>(r => r.InstanceType == InstanceType.Lidarr)), Times.Once);
    }

    #endregion

    #region Helper Methods

    private static DownloadHuntRequest<SearchItem> CreateHuntRequest()
    {
        return new DownloadHuntRequest<SearchItem>
        {
            InstanceType = InstanceType.Radarr,
            Instance = CreateArrInstance(),
            SearchItem = new SearchItem { Id = 123 },
            Record = CreateQueueRecord(),
            JobRunId = Guid.NewGuid()
        };
    }

    private static ArrInstance CreateArrInstance()
    {
        return new ArrInstance
        {
            Name = "Test Instance",
            Url = new Uri("http://radarr.local"),
            ApiKey = "test-api-key"
        };
    }

    private static QueueRecord CreateQueueRecord()
    {
        return new QueueRecord
        {
            Id = 1,
            Title = "Test Record",
            Protocol = "torrent",
            DownloadId = "ABC123"
        };
    }

    private static Mock<ConsumeContext<DownloadHuntRequest<SearchItem>>> CreateConsumeContextMock(DownloadHuntRequest<SearchItem> message)
    {
        var mock = new Mock<ConsumeContext<DownloadHuntRequest<SearchItem>>>();
        mock.Setup(c => c.Message).Returns(message);
        return mock;
    }

    #endregion
}
