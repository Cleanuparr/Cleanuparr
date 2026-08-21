using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.LazyLibrarian;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.LazyLibrarian;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.MalwareBlocker;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.LazyLibrarian;

public class LazyLibrarianJobEvaluatorTests
{
    private readonly ILazyLibrarianService _lazyLibrarianService = Substitute.For<ILazyLibrarianService>();
    private readonly ArrInstance _instance = new()
    {
        Name = "lazylibrarian",
        Url = new Uri("http://localhost:5299"),
        ApiKey = "api-key",
    };

    private LazyLibrarianServiceQC CreateQueueCleanerEvaluator() =>
        new(Substitute.For<ILogger<LazyLibrarianServiceQC>>(), _lazyLibrarianService);

    private LazyLibrarianServiceCB CreateMalwareBlockerEvaluator() =>
        new(Substitute.For<ILogger<LazyLibrarianServiceCB>>(), _lazyLibrarianService);

    private static LazyLibrarianQueueItem CreateItem(string downloadId = "HASH1") => new()
    {
        DownloadId = downloadId,
        Title = "A Book",
        Books = [new LazyLibrarianBookRef { BookId = "OL7353617M", Library = BookLibrary.EBook }],
        Source = LazyLibrarianSource.QBittorrent,
        Origin = LazyLibrarianOrigin.New,
    };

    private static IDownloadService CreateClient(DownloadClientType type = DownloadClientType.Torrent)
    {
        IDownloadService service = Substitute.For<IDownloadService>();
        service.ClientConfig.Returns(new DownloadClientConfig
        {
            Id = Guid.NewGuid(),
            Name = "Test Client",
            Type = type,
            TypeName = DownloadClientTypeName.qBittorrent,
            Enabled = true,
            Host = new Uri("http://localhost:8080"),
        });

        return service;
    }

    private void StubQueue(params LazyLibrarianQueueItem[] items) =>
        _lazyLibrarianService.GetQueueAsync(_instance).Returns(items);

    [Fact]
    public async Task EvaluateAsync_WithoutATorrentClient_ReturnsNothing()
    {
        // Arrange: an usenet client cannot hold a LazyLibrarian torrent snatch.
        StubQueue(CreateItem());
        IDownloadService usenet = CreateClient(DownloadClientType.Usenet);

        // Act
        IReadOnlyList<LazyLibrarianRemovalDecision> decisions =
            await CreateQueueCleanerEvaluator().EvaluateAsync(_instance, [usenet], []);

        // Assert
        decisions.ShouldBeEmpty();
        await usenet.DidNotReceive().ShouldRemoveFromArrQueueAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>());
    }

    [Fact]
    public async Task EvaluateAsync_SkipsAnIgnoredDownload()
    {
        // Arrange
        StubQueue(CreateItem());
        IDownloadService client = CreateClient();

        // Act
        IReadOnlyList<LazyLibrarianRemovalDecision> decisions =
            await CreateQueueCleanerEvaluator().EvaluateAsync(_instance, [client], ["hash1"]);

        // Assert
        decisions.ShouldBeEmpty();
        await client.DidNotReceive().ShouldRemoveFromArrQueueAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>());
    }

    [Fact]
    public async Task EvaluateAsync_WhenNoClientHoldsTheDownload_ReturnsNothing()
    {
        // Arrange
        StubQueue(CreateItem());
        IDownloadService client = CreateClient();
        client.ShouldRemoveFromArrQueueAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>())
            .Returns(new DownloadCheckResult { Found = false });

        // Act
        IReadOnlyList<LazyLibrarianRemovalDecision> decisions =
            await CreateQueueCleanerEvaluator().EvaluateAsync(_instance, [client], []);

        // Assert
        decisions.ShouldBeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_WhenTheClientKeepsTheDownload_ReturnsNothing()
    {
        // Arrange
        StubQueue(CreateItem());
        IDownloadService client = CreateClient();
        client.ShouldRemoveFromArrQueueAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>())
            .Returns(new DownloadCheckResult { Found = true, ShouldRemove = false });

        // Act
        IReadOnlyList<LazyLibrarianRemovalDecision> decisions =
            await CreateQueueCleanerEvaluator().EvaluateAsync(_instance, [client], []);

        // Assert
        decisions.ShouldBeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_AsksTheNextClientWhenOneThrows()
    {
        // Arrange
        StubQueue(CreateItem());
        ITorrentItemWrapper torrent = Substitute.For<ITorrentItemWrapper>();

        IDownloadService broken = CreateClient();
        broken.ShouldRemoveFromArrQueueAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>())
            .Throws(new Exception("client is down"));

        IDownloadService healthy = CreateClient();
        healthy.ShouldRemoveFromArrQueueAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>())
            .Returns(new DownloadCheckResult
            {
                Found = true,
                ShouldRemove = true,
                DeleteReason = DeleteReason.Stalled,
                Torrent = torrent,
            });

        // Act
        IReadOnlyList<LazyLibrarianRemovalDecision> decisions =
            await CreateQueueCleanerEvaluator().EvaluateAsync(_instance, [broken, healthy], []);

        // Assert
        LazyLibrarianRemovalDecision decision = decisions.ShouldHaveSingleItem();
        decision.DownloadService.ShouldBe(healthy);
        decision.DownloadClient.ShouldBe(healthy.ClientConfig);
        decision.Torrent.ShouldBe(torrent);
        decision.DeleteReason.ShouldBe(DeleteReason.Stalled);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    [InlineData(false, false, true)]
    public async Task EvaluateAsync_QueueCleaner_HonoursThePrivateTorrentRule(
        bool isPrivate,
        bool deleteFromClient,
        bool expectedRemoveFromClient
    )
    {
        // Arrange
        StubQueue(CreateItem());
        IDownloadService client = CreateClient();
        client.ShouldRemoveFromArrQueueAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>())
            .Returns(new DownloadCheckResult
            {
                Found = true,
                ShouldRemove = true,
                IsPrivate = isPrivate,
                DeleteFromClient = deleteFromClient,
                Torrent = Substitute.For<ITorrentItemWrapper>(),
            });

        // Act
        IReadOnlyList<LazyLibrarianRemovalDecision> decisions =
            await CreateQueueCleanerEvaluator().EvaluateAsync(_instance, [client], []);

        // Assert
        decisions.ShouldHaveSingleItem().RemoveFromClient.ShouldBe(expectedRemoveFromClient);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    [InlineData(false, false, true)]
    public async Task EvaluateAsync_MalwareBlocker_HonoursTheDeletePrivateSetting(
        bool isPrivate,
        bool deletePrivate,
        bool expectedRemoveFromClient
    )
    {
        // Arrange
        ContextProvider.Set(nameof(ContentBlockerConfig), new ContentBlockerConfig { DeletePrivate = deletePrivate });
        StubQueue(CreateItem());

        IDownloadService client = CreateClient();
        client.BlockUnwantedFilesAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>())
            .Returns(new BlockFilesResult
            {
                Found = true,
                ShouldRemove = true,
                IsPrivate = isPrivate,
                DeleteReason = DeleteReason.AllFilesBlocked,
                Torrent = Substitute.For<ITorrentItemWrapper>(),
            });

        // Act
        IReadOnlyList<LazyLibrarianRemovalDecision> decisions =
            await CreateMalwareBlockerEvaluator().EvaluateAsync(_instance, [client], []);

        // Assert
        LazyLibrarianRemovalDecision decision = decisions.ShouldHaveSingleItem();
        decision.RemoveFromClient.ShouldBe(expectedRemoveFromClient);
        decision.DeleteReason.ShouldBe(DeleteReason.AllFilesBlocked);
    }

    [Fact]
    public async Task EvaluateAsync_EvaluatesEveryQueuedDownload()
    {
        // Arrange
        StubQueue(CreateItem("HASH1"), CreateItem("HASH2"));
        IDownloadService client = CreateClient();
        client.ShouldRemoveFromArrQueueAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>())
            .Returns(new DownloadCheckResult
            {
                Found = true,
                ShouldRemove = true,
                Torrent = Substitute.For<ITorrentItemWrapper>(),
            });

        // Act
        IReadOnlyList<LazyLibrarianRemovalDecision> decisions =
            await CreateQueueCleanerEvaluator().EvaluateAsync(_instance, [client], []);

        // Assert
        decisions.Select(decision => decision.Item.DownloadId).ShouldBe(["HASH1", "HASH2"]);
    }
}
