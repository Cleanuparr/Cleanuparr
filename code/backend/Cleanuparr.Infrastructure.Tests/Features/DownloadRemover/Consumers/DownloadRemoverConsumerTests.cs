using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadRemover.Consumers;
using Cleanuparr.Infrastructure.Features.DownloadRemover.Interfaces;
using Cleanuparr.Infrastructure.Features.DownloadRemover.Models;
using Cleanuparr.Infrastructure.Features.DryRun;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration.Arr;
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
    private readonly DryRunActivity _dryRunActivity;
    private readonly DownloadRemoverConsumer _consumer;

    public DownloadRemoverConsumerTests()
    {
        _logger = Substitute.For<ILogger<DownloadRemoverConsumer>>();
        _queueItemRemover = Substitute.For<IQueueItemRemover>();
        _dryRunActivity = new DryRunActivity();
        _consumer = new DownloadRemoverConsumer(_logger, _queueItemRemover, _dryRunActivity);
    }

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_CallsRemoveQueueItemAsync()
    {
        // Arrange
        var request = CreateRemoveRequest();

        _queueItemRemover
            .RemoveQueueItemAsync(Arg.Any<QueueItemRemoveRequest>())
            .Returns(Task.CompletedTask);

        // Act
        await _consumer.HandleAsync(request);

        // Assert
        await _queueItemRemover.Received(1).RemoveQueueItemAsync(request);
    }

    [Fact]
    public async Task HandleAsync_WhenRemoverThrows_LogsErrorAndDoesNotRethrow()
    {
        // Arrange
        var request = CreateRemoveRequest();

        _queueItemRemover
            .RemoveQueueItemAsync(Arg.Any<QueueItemRemoveRequest>())
            .ThrowsAsync(new Exception("Remove failed"));

        // Act - Should not throw
        await _consumer.HandleAsync(request);

        // Assert
        _logger.HasLogContaining(LogLevel.Error, "failed to remove queue item").ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_PassesCorrectRequestToRemover()
    {
        // Arrange
        var request = CreateRemoveRequest();
        QueueItemRemoveRequest? capturedRequest = null;

        _queueItemRemover
            .RemoveQueueItemAsync(Arg.Any<QueueItemRemoveRequest>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedRequest = ci.Arg<QueueItemRemoveRequest>());

        // Act
        await _consumer.HandleAsync(request);

        // Assert
        capturedRequest.ShouldNotBeNull();
        capturedRequest.Instance.ArrConfig.Type.ShouldBe(request.Instance.ArrConfig.Type);
        capturedRequest.ArrTarget().SearchItem.Id.ShouldBe(request.ArrTarget().SearchItem.Id);
        capturedRequest.ArrTarget().RemoveFromClient.ShouldBe(request.ArrTarget().RemoveFromClient);
        capturedRequest.DeleteReason.ShouldBe(request.DeleteReason);
    }

    [Fact]
    public async Task HandleAsync_WithDryRunRequest_SetsStickyDryRunBeforeRemoving()
    {
        // Arrange
        var request = CreateRemoveRequest() with { IsDryRun = true };
        bool observedDryRun = false;

        _queueItemRemover
            .RemoveQueueItemAsync(Arg.Any<QueueItemRemoveRequest>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => observedDryRun = ContextProvider.IsDryRunSticky());

        // Act
        await _consumer.HandleAsync(request);

        // Assert
        observedDryRun.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_WithDryRunRequest_RequestsAPurgeAfterRemoving()
    {
        // Arrange
        var request = CreateRemoveRequest() with { IsDryRun = true };

        _queueItemRemover
            .RemoveQueueItemAsync(Arg.Any<QueueItemRemoveRequest>())
            .Returns(Task.CompletedTask);

        // Act
        await _consumer.HandleAsync(request);

        // Assert
        (await _dryRunActivity.WaitForPurgeRequestAsync(CancellationToken.None)).ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_WithLiveRequest_RequestsNoPurge()
    {
        // Arrange
        var request = CreateRemoveRequest() with { IsDryRun = false };

        _queueItemRemover
            .RemoveQueueItemAsync(Arg.Any<QueueItemRemoveRequest>())
            .Returns(Task.CompletedTask);

        // Act
        await _consumer.HandleAsync(request);

        // Assert
        Task<bool> waitTask = _dryRunActivity.WaitForPurgeRequestAsync(CancellationToken.None).AsTask();
        await Task.Yield();
        waitTask.IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleAsync_WithDryRunRequest_WhenRemoverThrows_ExitsActivity()
    {
        // Arrange
        QueueItemRemoveRequest request = CreateRemoveRequest() with { IsDryRun = true };

        _queueItemRemover
            .RemoveQueueItemAsync(Arg.Any<QueueItemRemoveRequest>())
            .ThrowsAsync(new Exception("Remove failed"));

        // Act
        await _consumer.HandleAsync(request);

        // Assert
        _dryRunActivity.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleAsync_WithLiveRequest_NeverEntersActivity()
    {
        // Arrange
        QueueItemRemoveRequest request = CreateRemoveRequest() with { IsDryRun = false };
        bool activeDuringRemoval = false;

        _queueItemRemover
            .RemoveQueueItemAsync(Arg.Any<QueueItemRemoveRequest>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => activeDuringRemoval = _dryRunActivity.IsActive);

        // Act
        await _consumer.HandleAsync(request);

        // Assert
        activeDuringRemoval.ShouldBeFalse();
        _dryRunActivity.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleAsync_WithRemoveFromClientTrue_PassesCorrectly()
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
            JobRunId = Guid.NewGuid(),
            IsDryRun = false,
        };

        _queueItemRemover
            .RemoveQueueItemAsync(Arg.Any<QueueItemRemoveRequest>())
            .Returns(Task.CompletedTask);

        // Act
        await _consumer.HandleAsync(request);

        // Assert
        await _queueItemRemover.Received(1).RemoveQueueItemAsync(
            Arg.Is<QueueItemRemoveRequest>(req =>
                req.ArrTarget().RemoveFromClient == true &&
                req.DeleteReason == DeleteReason.Stalled));
    }

    [Fact]
    public async Task HandleAsync_WithDifferentDeleteReasons_HandlesCorrectly()
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
            JobRunId = Guid.NewGuid(),
            IsDryRun = false,
        };

        _queueItemRemover
            .RemoveQueueItemAsync(Arg.Any<QueueItemRemoveRequest>())
            .Returns(Task.CompletedTask);

        // Act
        await _consumer.HandleAsync(request);

        // Assert
        await _queueItemRemover.Received(1).RemoveQueueItemAsync(
            Arg.Is<QueueItemRemoveRequest>(req =>
                req.DeleteReason == DeleteReason.FailedImport));
    }

    [Fact]
    public async Task HandleAsync_WithDifferentInstanceTypes_HandlesCorrectly()
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
            JobRunId = Guid.NewGuid(),
            IsDryRun = false,
        };

        _queueItemRemover
            .RemoveQueueItemAsync(Arg.Any<QueueItemRemoveRequest>())
            .Returns(Task.CompletedTask);

        // Act
        await _consumer.HandleAsync(request);

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
            JobRunId = Guid.NewGuid(),
            IsDryRun = false,
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


    #endregion
}
