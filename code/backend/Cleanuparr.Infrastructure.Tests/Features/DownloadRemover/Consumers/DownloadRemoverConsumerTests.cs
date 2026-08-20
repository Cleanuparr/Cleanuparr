using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadRemover.Consumers;
using Cleanuparr.Infrastructure.Features.DownloadRemover.Interfaces;
using Cleanuparr.Infrastructure.Features.DownloadRemover.Models;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using MassTransit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadRemover.Consumers;

public class DownloadRemoverConsumerTests
{
    private readonly ILogger<DownloadRemoverConsumer> _logger;
    private readonly IQueueItemRemover _queueItemRemover;
    private readonly DownloadRemoverConsumer _consumer;

    public DownloadRemoverConsumerTests()
    {
        _logger = Substitute.For<ILogger<DownloadRemoverConsumer>>();
        _queueItemRemover = Substitute.For<IQueueItemRemover>();
        _consumer = new DownloadRemoverConsumer(_logger, _queueItemRemover);
    }

    #region Consume Tests

    [Fact]
    public async Task Consume_CallsRemoveQueueItemAsync()
    {
        // Arrange
        var request = CreateRemoveRequest();
        var context = CreateConsumeContext(request);

        _queueItemRemover
            .RemoveQueueItemAsync(Arg.Any<QueueItemRemoveRequest>())
            .Returns(Task.CompletedTask);

        // Act
        await _consumer.Consume(context);

        // Assert
        await _queueItemRemover.Received(1).RemoveQueueItemAsync(request);
    }

    [Fact]
    public async Task Consume_WhenRemoverThrows_LogsErrorAndDoesNotRethrow()
    {
        // Arrange
        var request = CreateRemoveRequest();
        var context = CreateConsumeContext(request);

        _queueItemRemover
            .RemoveQueueItemAsync(Arg.Any<QueueItemRemoveRequest>())
            .ThrowsAsync(new Exception("Remove failed"));

        // Act - Should not throw
        await _consumer.Consume(context);

        // Assert
        _logger.ReceivedLogContaining(LogLevel.Error, "failed to remove queue item");
    }

    [Fact]
    public async Task Consume_PassesCorrectRequestToRemover()
    {
        // Arrange
        var request = CreateRemoveRequest();
        var context = CreateConsumeContext(request);
        QueueItemRemoveRequest? capturedRequest = null;

        _queueItemRemover
            .RemoveQueueItemAsync(Arg.Any<QueueItemRemoveRequest>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedRequest = ci.Arg<QueueItemRemoveRequest>());

        // Act
        await _consumer.Consume(context);

        // Assert
        capturedRequest.ShouldNotBeNull();
        capturedRequest.Instance.ArrConfig.Type.ShouldBe(request.Instance.ArrConfig.Type);
        capturedRequest.ArrTarget().SearchItem.Id.ShouldBe(request.ArrTarget().SearchItem.Id);
        capturedRequest.ArrTarget().RemoveFromClient.ShouldBe(request.ArrTarget().RemoveFromClient);
        capturedRequest.DeleteReason.ShouldBe(request.DeleteReason);
    }

    [Fact]
    public async Task Consume_WithRemoveFromClientTrue_PassesCorrectly()
    {
        // Arrange
        var request = new QueueItemRemoveRequest
        {
            Instance = CreateArrInstance(InstanceType.Sonarr),
            Target = new ArrRemovalTarget
            {
                Record = CreateQueueRecord(),
                SearchItem = new SearchItem { Id = 456 },
                RemoveFromClient = true,
            },
            DeleteReason = DeleteReason.Stalled,
            JobRunId = Guid.NewGuid()
        };
        var context = CreateConsumeContext(request);

        _queueItemRemover
            .RemoveQueueItemAsync(Arg.Any<QueueItemRemoveRequest>())
            .Returns(Task.CompletedTask);

        // Act
        await _consumer.Consume(context);

        // Assert
        await _queueItemRemover.Received(1).RemoveQueueItemAsync(
            Arg.Is<QueueItemRemoveRequest>(req =>
                req.ArrTarget().RemoveFromClient == true &&
                req.DeleteReason == DeleteReason.Stalled));
    }

    [Fact]
    public async Task Consume_WithDifferentDeleteReasons_HandlesCorrectly()
    {
        // Arrange
        var request = new QueueItemRemoveRequest
        {
            Instance = CreateArrInstance(InstanceType.Radarr),
            Target = new ArrRemovalTarget
            {
                Record = CreateQueueRecord(),
                SearchItem = new SearchItem { Id = 789 },
                RemoveFromClient = false,
            },
            DeleteReason = DeleteReason.FailedImport,
            JobRunId = Guid.NewGuid()
        };
        var context = CreateConsumeContext(request);

        _queueItemRemover
            .RemoveQueueItemAsync(Arg.Any<QueueItemRemoveRequest>())
            .Returns(Task.CompletedTask);

        // Act
        await _consumer.Consume(context);

        // Assert
        await _queueItemRemover.Received(1).RemoveQueueItemAsync(
            Arg.Is<QueueItemRemoveRequest>(req =>
                req.DeleteReason == DeleteReason.FailedImport));
    }

    [Fact]
    public async Task Consume_WithDifferentInstanceTypes_HandlesCorrectly()
    {
        // Arrange
        var request = new QueueItemRemoveRequest
        {
            Instance = CreateArrInstance(InstanceType.Readarr),
            Target = new ArrRemovalTarget
            {
                Record = CreateQueueRecord(),
                SearchItem = new SearchItem { Id = 111 },
                RemoveFromClient = true,
            },
            DeleteReason = DeleteReason.SlowSpeed,
            JobRunId = Guid.NewGuid()
        };
        var context = CreateConsumeContext(request);

        _queueItemRemover
            .RemoveQueueItemAsync(Arg.Any<QueueItemRemoveRequest>())
            .Returns(Task.CompletedTask);

        // Act
        await _consumer.Consume(context);

        // Assert
        await _queueItemRemover.Received(1).RemoveQueueItemAsync(
            Arg.Is<QueueItemRemoveRequest>(req => req.Instance.ArrConfig.Type == InstanceType.Readarr));
    }

    #endregion

    #region Helper Methods

    private static QueueItemRemoveRequest CreateRemoveRequest()
    {
        return new QueueItemRemoveRequest
        {
            Instance = CreateArrInstance(InstanceType.Radarr),
            Target = new ArrRemovalTarget
            {
                Record = CreateQueueRecord(),
                SearchItem = new SearchItem { Id = 123 },
                RemoveFromClient = true,
            },
            DeleteReason = DeleteReason.Stalled,
            JobRunId = Guid.NewGuid()
        };
    }

    private static ArrInstance CreateArrInstance(InstanceType instanceType = InstanceType.Radarr)
    {
        return new ArrInstance
        {
            Name = "Test Instance",
            Url = new Uri("http://radarr.local"),
            ApiKey = "test-api-key",
            ArrConfig = new ArrConfig { Type = instanceType }
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

    private static ConsumeContext<QueueItemRemoveRequest> CreateConsumeContext(QueueItemRemoveRequest message)
    {
        var context = Substitute.For<ConsumeContext<QueueItemRemoveRequest>>();
        context.Message.Returns(message);
        return context;
    }

    #endregion
}
