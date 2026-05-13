using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Infrastructure.Features.Arr;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Arr;

public class ArrQueueIteratorTests
{
    private readonly ILogger<ArrQueueIterator> _logger;
    private readonly IArrClient _arrClient;
    private readonly ArrInstance _arrInstance;
    private readonly ArrQueueIterator _iterator;

    public ArrQueueIteratorTests()
    {
        _logger = Substitute.For<ILogger<ArrQueueIterator>>();
        _arrClient = Substitute.For<IArrClient>();
        _arrInstance = new ArrInstance
        {
            Name = "test",
            Url = new Uri("http://localhost:8989"),
            ApiKey = "key",
        };
        _iterator = new ArrQueueIterator(_logger);
    }

    [Fact]
    public async Task Iterate_EmptyQueue_DoesNotInvokeAction()
    {
        // Arrange
        _arrClient.GetQueueItemsAsync(_arrInstance, Arg.Any<int>())
            .Returns(new QueueListResponse { TotalRecords = 0, Records = Array.Empty<QueueRecord>() });
        int invocations = 0;

        // Act
        await _iterator.Iterate(_arrClient, _arrInstance, _ =>
        {
            invocations++;
            return Task.CompletedTask;
        });

        // Assert
        invocations.ShouldBe(0);
        await _arrClient.Received(1).GetQueueItemsAsync(_arrInstance, 1);
    }

    [Fact]
    public async Task Iterate_SinglePage_InvokesActionOnce()
    {
        // Arrange
        var records = new[] { BuildRecord(1), BuildRecord(2) };
        _arrClient.GetQueueItemsAsync(_arrInstance, 1)
            .Returns(new QueueListResponse { TotalRecords = records.Length, Records = records });
        int invocations = 0;
        IReadOnlyList<QueueRecord>? captured = null;

        // Act
        await _iterator.Iterate(_arrClient, _arrInstance, batch =>
        {
            invocations++;
            captured = batch;
            return Task.CompletedTask;
        });

        // Assert
        invocations.ShouldBe(1);
        captured.ShouldNotBeNull();
        captured!.Count.ShouldBe(2);
        await _arrClient.Received(1).GetQueueItemsAsync(_arrInstance, 1);
        await _arrClient.DidNotReceive().GetQueueItemsAsync(_arrInstance, 2);
    }

    [Fact]
    public async Task Iterate_MultiPage_AdvancesPageAndInvokesActionPerPage()
    {
        // Arrange — 5 total records, 2 per page
        _arrClient.GetQueueItemsAsync(_arrInstance, 1)
            .Returns(new QueueListResponse { TotalRecords = 5, Records = new[] { BuildRecord(1), BuildRecord(2) } });
        _arrClient.GetQueueItemsAsync(_arrInstance, 2)
            .Returns(new QueueListResponse { TotalRecords = 5, Records = new[] { BuildRecord(3), BuildRecord(4) } });
        _arrClient.GetQueueItemsAsync(_arrInstance, 3)
            .Returns(new QueueListResponse { TotalRecords = 5, Records = new[] { BuildRecord(5) } });
        int invocations = 0;

        // Act
        await _iterator.Iterate(_arrClient, _arrInstance, _ =>
        {
            invocations++;
            return Task.CompletedTask;
        });

        // Assert
        invocations.ShouldBe(3);
        await _arrClient.Received(1).GetQueueItemsAsync(_arrInstance, 1);
        await _arrClient.Received(1).GetQueueItemsAsync(_arrInstance, 2);
        await _arrClient.Received(1).GetQueueItemsAsync(_arrInstance, 3);
        await _arrClient.DidNotReceive().GetQueueItemsAsync(_arrInstance, 4);
    }

    [Fact]
    public async Task Iterate_StopsWhenProcessedReachesTotal()
    {
        // Arrange — total reported as 2, server returns 2 on page 1; iterator must not request page 2
        _arrClient.GetQueueItemsAsync(_arrInstance, 1)
            .Returns(new QueueListResponse { TotalRecords = 2, Records = new[] { BuildRecord(1), BuildRecord(2) } });

        // Act
        await _iterator.Iterate(_arrClient, _arrInstance, _ => Task.CompletedTask);

        // Assert
        await _arrClient.Received(1).GetQueueItemsAsync(_arrInstance, Arg.Any<int>());
    }

    [Fact]
    public async Task Iterate_EmptyMidPagination_BreaksWithoutCallingAction()
    {
        // Arrange — first page has records, second page is empty (should stop without invoking action again)
        _arrClient.GetQueueItemsAsync(_arrInstance, 1)
            .Returns(new QueueListResponse { TotalRecords = 99, Records = new[] { BuildRecord(1) } });
        _arrClient.GetQueueItemsAsync(_arrInstance, 2)
            .Returns(new QueueListResponse { TotalRecords = 99, Records = Array.Empty<QueueRecord>() });
        int invocations = 0;

        // Act
        await _iterator.Iterate(_arrClient, _arrInstance, _ =>
        {
            invocations++;
            return Task.CompletedTask;
        });

        // Assert
        invocations.ShouldBe(1);
        await _arrClient.Received(1).GetQueueItemsAsync(_arrInstance, 1);
        await _arrClient.Received(1).GetQueueItemsAsync(_arrInstance, 2);
        await _arrClient.DidNotReceive().GetQueueItemsAsync(_arrInstance, 3);
    }

    [Fact]
    public async Task Iterate_PassesRecordsToActionWithoutMutation()
    {
        // Arrange
        var records = new[] { BuildRecord(7), BuildRecord(8) };
        _arrClient.GetQueueItemsAsync(_arrInstance, 1)
            .Returns(new QueueListResponse { TotalRecords = 2, Records = records });
        IReadOnlyList<QueueRecord>? observed = null;

        // Act
        await _iterator.Iterate(_arrClient, _arrInstance, batch =>
        {
            observed = batch;
            return Task.CompletedTask;
        });

        // Assert
        observed.ShouldNotBeNull();
        observed!.Select(r => r.Id).ShouldBe(new long[] { 7, 8 });
    }

    private static QueueRecord BuildRecord(long id) => new()
    {
        Id = id,
        Title = $"item-{id}",
        DownloadId = id.ToString(),
        Protocol = "torrent",
    };
}
