using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Infrastructure.Features.Arr;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Arr;

public class ArrQueueReaderTests
{
    private readonly ILogger<ArrQueueReader> _logger;
    private readonly IArrClient _arrClient;
    private readonly ArrInstance _arrInstance;
    private readonly ArrQueueReader _reader;

    public ArrQueueReaderTests()
    {
        _logger = Substitute.For<ILogger<ArrQueueReader>>();
        _arrClient = Substitute.For<IArrClient>();
        _arrInstance = new ArrInstance
        {
            Name = "test",
            Url = new Uri("http://localhost:8989"),
            ApiKey = "key",
        };
        _reader = new ArrQueueReader(_logger);
    }

    [Fact]
    public async Task ReadAllAsync_EmptyQueue_ReturnsEmptyList()
    {
        // Arrange
        _arrClient.GetQueueItemsAsync(_arrInstance, Arg.Any<int>())
            .Returns(new QueueListResponse { TotalRecords = 0, Records = Array.Empty<QueueRecord>() });

        // Act
        List<QueueRecord> result = await _reader.ReadAllAsync(_arrClient, _arrInstance);

        // Assert
        result.ShouldBeEmpty();
        await _arrClient.Received(1).GetQueueItemsAsync(_arrInstance, 1);
    }

    [Fact]
    public async Task ReadAllAsync_SinglePage_ReturnsAllRecords()
    {
        // Arrange
        QueueRecord[] records = [BuildRecord(1), BuildRecord(2)];
        _arrClient.GetQueueItemsAsync(_arrInstance, 1)
            .Returns(new QueueListResponse { TotalRecords = records.Length, Records = records });

        // Act
        List<QueueRecord> result = await _reader.ReadAllAsync(_arrClient, _arrInstance);

        // Assert
        result.Count.ShouldBe(2);
        await _arrClient.Received(1).GetQueueItemsAsync(_arrInstance, 1);
        await _arrClient.DidNotReceive().GetQueueItemsAsync(_arrInstance, 2);
    }

    [Fact]
    public async Task ReadAllAsync_MultiPage_ReturnsConcatenatedRecords()
    {
        // Arrange: 5 total records, 2 per page
        _arrClient.GetQueueItemsAsync(_arrInstance, 1)
            .Returns(new QueueListResponse { TotalRecords = 5, Records = new[] { BuildRecord(1), BuildRecord(2) } });
        _arrClient.GetQueueItemsAsync(_arrInstance, 2)
            .Returns(new QueueListResponse { TotalRecords = 5, Records = new[] { BuildRecord(3), BuildRecord(4) } });
        _arrClient.GetQueueItemsAsync(_arrInstance, 3)
            .Returns(new QueueListResponse { TotalRecords = 5, Records = new[] { BuildRecord(5) } });

        // Act
        List<QueueRecord> result = await _reader.ReadAllAsync(_arrClient, _arrInstance);

        // Assert
        result.Select(r => r.Id).ShouldBe(new long[] { 1, 2, 3, 4, 5 });
        await _arrClient.Received(1).GetQueueItemsAsync(_arrInstance, 1);
        await _arrClient.Received(1).GetQueueItemsAsync(_arrInstance, 2);
        await _arrClient.Received(1).GetQueueItemsAsync(_arrInstance, 3);
        await _arrClient.DidNotReceive().GetQueueItemsAsync(_arrInstance, 4);
    }

    [Fact]
    public async Task ReadAllAsync_DownloadIdSpansPages_BothRecordsAppearInResult()
    {
        // Arrange: same download id split across page 1 and page 2
        QueueRecord page1Record = BuildRecord(1, downloadId: "shared-download");
        QueueRecord page2Record = BuildRecord(2, downloadId: "shared-download");
        _arrClient.GetQueueItemsAsync(_arrInstance, 1)
            .Returns(new QueueListResponse { TotalRecords = 2, Records = new[] { page1Record } });
        _arrClient.GetQueueItemsAsync(_arrInstance, 2)
            .Returns(new QueueListResponse { TotalRecords = 2, Records = new[] { page2Record } });

        // Act
        List<QueueRecord> result = await _reader.ReadAllAsync(_arrClient, _arrInstance);

        // Assert
        result.Count(r => r.DownloadId == "shared-download").ShouldBe(2);
        result.Select(r => r.Id).ShouldBe(new long[] { 1, 2 });
    }

    [Fact]
    public async Task ReadAllAsync_StopsWhenProcessedReachesTotal()
    {
        // Arrange: total reported as 2, server returns 2 on page 1; reader must not request page 2
        _arrClient.GetQueueItemsAsync(_arrInstance, 1)
            .Returns(new QueueListResponse { TotalRecords = 2, Records = new[] { BuildRecord(1), BuildRecord(2) } });

        // Act
        List<QueueRecord> result = await _reader.ReadAllAsync(_arrClient, _arrInstance);

        // Assert
        result.Count.ShouldBe(2);
        await _arrClient.Received(1).GetQueueItemsAsync(_arrInstance, Arg.Any<int>());
    }

    [Fact]
    public async Task ReadAllAsync_EmptyMidPagination_StopsWithoutFurtherPages()
    {
        // Arrange: first page has records, second page is empty (should stop without requesting a third page)
        _arrClient.GetQueueItemsAsync(_arrInstance, 1)
            .Returns(new QueueListResponse { TotalRecords = 99, Records = new[] { BuildRecord(1) } });
        _arrClient.GetQueueItemsAsync(_arrInstance, 2)
            .Returns(new QueueListResponse { TotalRecords = 99, Records = Array.Empty<QueueRecord>() });

        // Act
        List<QueueRecord> result = await _reader.ReadAllAsync(_arrClient, _arrInstance);

        // Assert
        result.Count.ShouldBe(1);
        await _arrClient.Received(1).GetQueueItemsAsync(_arrInstance, 1);
        await _arrClient.Received(1).GetQueueItemsAsync(_arrInstance, 2);
        await _arrClient.DidNotReceive().GetQueueItemsAsync(_arrInstance, 3);
    }

    [Fact]
    public async Task ReadAllAsync_ReturnsRecordsInOrderWithoutMutation()
    {
        // Arrange
        QueueRecord[] records = [BuildRecord(7), BuildRecord(8)];
        _arrClient.GetQueueItemsAsync(_arrInstance, 1)
            .Returns(new QueueListResponse { TotalRecords = 2, Records = records });

        // Act
        List<QueueRecord> result = await _reader.ReadAllAsync(_arrClient, _arrInstance);

        // Assert
        result.Select(r => r.Id).ShouldBe(new long[] { 7, 8 });
    }

    private static QueueRecord BuildRecord(long id, string? downloadId = null) => new()
    {
        Id = id,
        Title = $"item-{id}",
        DownloadId = downloadId ?? id.ToString(),
        Protocol = "torrent",
    };
}
