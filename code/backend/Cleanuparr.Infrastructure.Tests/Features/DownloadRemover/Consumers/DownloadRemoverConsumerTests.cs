using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadRemover.Consumers;
using Cleanuparr.Infrastructure.Features.DownloadRemover.Interfaces;
using Cleanuparr.Infrastructure.Features.DownloadRemover.Models;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Data.Models.Arr;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadRemover.Consumers;

public class DownloadRemoverConsumerTests
{
    private readonly Mock<ILogger<DownloadRemoverConsumer<SearchItem>>> _loggerMock;
    private readonly Mock<IQueueItemRemover> _queueItemRemoverMock;
    private readonly DownloadRemoverConsumer<SearchItem> _consumer;

    public DownloadRemoverConsumerTests()
    {
        _loggerMock = new Mock<ILogger<DownloadRemoverConsumer<SearchItem>>>();
        _queueItemRemoverMock = new Mock<IQueueItemRemover>();
        _consumer = new DownloadRemoverConsumer<SearchItem>(_loggerMock.Object, _queueItemRemoverMock.Object);
    }

    #region Consume Tests

    [Fact]
    public async Task Consume_CallsRemoveQueueItemAsync()
    {
        // Arrange
        var request = CreateRemoveRequest();
        var contextMock = CreateConsumeContextMock(request);

        _queueItemRemoverMock
            .Setup(r => r.RemoveQueueItemAsync(It.IsAny<QueueItemRemoveRequest<SearchItem>>()))
            .Returns(Task.CompletedTask);

        // Act
        await _consumer.Consume(contextMock.Object);

        // Assert
        _queueItemRemoverMock.Verify(r => r.RemoveQueueItemAsync(request), Times.Once);
    }

    [Fact]
    public async Task Consume_WhenRemoverThrows_LogsErrorAndDoesNotRethrow()
    {
        // Arrange
        var request = CreateRemoveRequest();
        var contextMock = CreateConsumeContextMock(request);

        _queueItemRemoverMock
            .Setup(r => r.RemoveQueueItemAsync(It.IsAny<QueueItemRemoveRequest<SearchItem>>()))
            .ThrowsAsync(new Exception("Remove failed"));

        // Act - Should not throw
        await _consumer.Consume(contextMock.Object);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("failed to remove queue item")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_PassesCorrectRequestToRemover()
    {
        // Arrange
        var request = CreateRemoveRequest();
        var contextMock = CreateConsumeContextMock(request);
        QueueItemRemoveRequest<SearchItem>? capturedRequest = null;

        _queueItemRemoverMock
            .Setup(r => r.RemoveQueueItemAsync(It.IsAny<QueueItemRemoveRequest<SearchItem>>()))
            .Callback<QueueItemRemoveRequest<SearchItem>>(r => capturedRequest = r)
            .Returns(Task.CompletedTask);

        // Act
        await _consumer.Consume(contextMock.Object);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal(request.InstanceType, capturedRequest.InstanceType);
        Assert.Equal(request.SearchItem.Id, capturedRequest.SearchItem.Id);
        Assert.Equal(request.RemoveFromClient, capturedRequest.RemoveFromClient);
        Assert.Equal(request.DeleteReason, capturedRequest.DeleteReason);
    }

    [Fact]
    public async Task Consume_WithRemoveFromClientTrue_PassesCorrectly()
    {
        // Arrange
        var request = new QueueItemRemoveRequest<SearchItem>
        {
            InstanceType = InstanceType.Sonarr,
            Instance = CreateArrInstance(),
            SearchItem = new SearchItem { Id = 456 },
            Record = CreateQueueRecord(),
            RemoveFromClient = true,
            DeleteReason = DeleteReason.Stalled
        };
        var contextMock = CreateConsumeContextMock(request);

        _queueItemRemoverMock
            .Setup(r => r.RemoveQueueItemAsync(It.IsAny<QueueItemRemoveRequest<SearchItem>>()))
            .Returns(Task.CompletedTask);

        // Act
        await _consumer.Consume(contextMock.Object);

        // Assert
        _queueItemRemoverMock.Verify(r => r.RemoveQueueItemAsync(
            It.Is<QueueItemRemoveRequest<SearchItem>>(req =>
                req.RemoveFromClient == true &&
                req.DeleteReason == DeleteReason.Stalled)), Times.Once);
    }

    [Fact]
    public async Task Consume_WithDifferentDeleteReasons_HandlesCorrectly()
    {
        // Arrange
        var request = new QueueItemRemoveRequest<SearchItem>
        {
            InstanceType = InstanceType.Radarr,
            Instance = CreateArrInstance(),
            SearchItem = new SearchItem { Id = 789 },
            Record = CreateQueueRecord(),
            RemoveFromClient = false,
            DeleteReason = DeleteReason.FailedImport
        };
        var contextMock = CreateConsumeContextMock(request);

        _queueItemRemoverMock
            .Setup(r => r.RemoveQueueItemAsync(It.IsAny<QueueItemRemoveRequest<SearchItem>>()))
            .Returns(Task.CompletedTask);

        // Act
        await _consumer.Consume(contextMock.Object);

        // Assert
        _queueItemRemoverMock.Verify(r => r.RemoveQueueItemAsync(
            It.Is<QueueItemRemoveRequest<SearchItem>>(req =>
                req.DeleteReason == DeleteReason.FailedImport)), Times.Once);
    }

    [Fact]
    public async Task Consume_WithDifferentInstanceTypes_HandlesCorrectly()
    {
        // Arrange
        var request = new QueueItemRemoveRequest<SearchItem>
        {
            InstanceType = InstanceType.Readarr,
            Instance = CreateArrInstance(),
            SearchItem = new SearchItem { Id = 111 },
            Record = CreateQueueRecord(),
            RemoveFromClient = true,
            DeleteReason = DeleteReason.SlowSpeed
        };
        var contextMock = CreateConsumeContextMock(request);

        _queueItemRemoverMock
            .Setup(r => r.RemoveQueueItemAsync(It.IsAny<QueueItemRemoveRequest<SearchItem>>()))
            .Returns(Task.CompletedTask);

        // Act
        await _consumer.Consume(contextMock.Object);

        // Assert
        _queueItemRemoverMock.Verify(r => r.RemoveQueueItemAsync(
            It.Is<QueueItemRemoveRequest<SearchItem>>(req => req.InstanceType == InstanceType.Readarr)), Times.Once);
    }

    #endregion

    #region Helper Methods

    private static QueueItemRemoveRequest<SearchItem> CreateRemoveRequest()
    {
        return new QueueItemRemoveRequest<SearchItem>
        {
            InstanceType = InstanceType.Radarr,
            Instance = CreateArrInstance(),
            SearchItem = new SearchItem { Id = 123 },
            Record = CreateQueueRecord(),
            RemoveFromClient = true,
            DeleteReason = DeleteReason.Stalled
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

    private static Mock<ConsumeContext<QueueItemRemoveRequest<SearchItem>>> CreateConsumeContextMock(QueueItemRemoveRequest<SearchItem> message)
    {
        var mock = new Mock<ConsumeContext<QueueItemRemoveRequest<SearchItem>>>();
        mock.Setup(c => c.Message).Returns(message);
        return mock;
    }

    #endregion
}
