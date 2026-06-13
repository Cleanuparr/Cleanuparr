using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadCleaner.Services;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Tests.Features.Jobs.TestHelpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Jobs;

public sealed class DeadTorrentServiceTests : IDisposable
{
    private readonly DataContext _dataContext;
    private readonly IStriker _striker;
    private readonly IDownloadService _downloadService;
    private readonly DownloadClientConfig _clientConfig;
    private readonly DeadTorrentService _sut;

    public DeadTorrentServiceTests()
    {
        _dataContext = TestDataContextFactory.Create(seedData: false);
        _striker = Substitute.For<IStriker>();
        _downloadService = Substitute.For<IDownloadService>();

        _clientConfig = new DownloadClientConfig
        {
            Id = Guid.NewGuid(),
            Name = "Test qBittorrent",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Enabled = true,
            Host = new Uri("http://localhost:8080"),
        };
        _downloadService.ClientConfig.Returns(_clientConfig);

        _dataContext.DownloadClients.Add(_clientConfig);
        _dataContext.SaveChanges();

        _sut = new DeadTorrentService(Substitute.For<ILogger<DeadTorrentService>>(), _dataContext, _striker);
    }

    public void Dispose()
    {
        _dataContext.Dispose();
        GC.SuppressFinalize(this);
    }

    private void AddConfig(bool enabled = true, int maxStrikes = 3, bool useTag = false, List<string>? categories = null)
    {
        _dataContext.DeadTorrentConfigs.Add(new DeadTorrentConfig
        {
            DownloadClientConfigId = _clientConfig.Id,
            Enabled = enabled,
            TargetCategory = "cleanuparr-dead",
            UseTag = useTag,
            MaxStrikes = maxStrikes,
            Categories = categories ?? ["movies"],
        });
        _dataContext.SaveChanges();
    }

    private static ITorrentItemWrapper CreateTorrent(string hash, string category, int? seederCount)
    {
        var torrent = Substitute.For<ITorrentItemWrapper>();
        torrent.Hash.Returns(hash);
        torrent.Name.Returns($"Test {hash}");
        torrent.Category.Returns(category);
        torrent.SeederCount.Returns(seederCount);
        torrent.Tags.Returns(Array.Empty<string>());
        return torrent;
    }

    [Fact]
    public async Task NoConfig_DoesNothing()
    {
        var downloads = new List<ITorrentItemWrapper> { CreateTorrent("hash1", "movies", 0) };

        await _sut.ProcessAsync(_downloadService, downloads);

        await _striker.DidNotReceiveWithAnyArgs().StrikeAndCheckLimit(default!, default!, default, default);
        await _downloadService.DidNotReceiveWithAnyArgs().ChangeTorrentCategoryAsync(default!, default!, default);
    }

    [Fact]
    public async Task Disabled_DoesNothing()
    {
        AddConfig(enabled: false);
        var downloads = new List<ITorrentItemWrapper> { CreateTorrent("hash1", "movies", 0) };

        await _sut.ProcessAsync(_downloadService, downloads);

        await _striker.DidNotReceiveWithAnyArgs().StrikeAndCheckLimit(default!, default!, default, default);
    }

    [Fact]
    public async Task ZeroSeeders_BelowThreshold_StrikesButDoesNotMove()
    {
        AddConfig();
        _striker.StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), StrikeType.DeadTorrent)
            .Returns(false);
        var downloads = new List<ITorrentItemWrapper> { CreateTorrent("hash1", "movies", 0) };

        await _sut.ProcessAsync(_downloadService, downloads);

        await _striker.Received(1).StrikeAndCheckLimit("hash1", Arg.Any<string>(), (ushort)3, StrikeType.DeadTorrent);
        await _downloadService.DidNotReceiveWithAnyArgs().ChangeTorrentCategoryAsync(default!, default!, default);
    }

    [Fact]
    public async Task ZeroSeeders_AtThreshold_MovesToCategory()
    {
        AddConfig();
        _striker.StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), StrikeType.DeadTorrent)
            .Returns(true);
        var torrent = CreateTorrent("hash1", "movies", 0);
        var downloads = new List<ITorrentItemWrapper> { torrent };

        await _sut.ProcessAsync(_downloadService, downloads);

        await _downloadService.Received(1).ChangeTorrentCategoryAsync(torrent, "cleanuparr-dead", false);
    }

    [Fact]
    public async Task WithSeeders_ResetsStrikesAndDoesNotMove()
    {
        AddConfig();
        var downloads = new List<ITorrentItemWrapper> { CreateTorrent("hash1", "movies", 5) };

        await _sut.ProcessAsync(_downloadService, downloads);

        await _striker.Received(1).ResetStrikeAsync("hash1", Arg.Any<string>(), StrikeType.DeadTorrent);
        await _striker.DidNotReceiveWithAnyArgs().StrikeAndCheckLimit(default!, default!, default, default);
        await _downloadService.DidNotReceiveWithAnyArgs().ChangeTorrentCategoryAsync(default!, default!, default);
    }

    [Fact]
    public async Task UnavailableSeederCount_IsDead_Strikes()
    {
        AddConfig();
        _striker.StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), StrikeType.DeadTorrent)
            .Returns(false);
        var downloads = new List<ITorrentItemWrapper> { CreateTorrent("hash1", "movies", null) };

        await _sut.ProcessAsync(_downloadService, downloads);

        await _striker.Received(1).StrikeAndCheckLimit("hash1", Arg.Any<string>(), (ushort)3, StrikeType.DeadTorrent);
        await _striker.DidNotReceiveWithAnyArgs().ResetStrikeAsync(default!, default!, default);
    }

    [Fact]
    public async Task NegativeSeederCount_IsDead_Strikes()
    {
        AddConfig();
        _striker.StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), StrikeType.DeadTorrent)
            .Returns(false);
        var downloads = new List<ITorrentItemWrapper> { CreateTorrent("hash1", "movies", -1) };

        await _sut.ProcessAsync(_downloadService, downloads);

        await _striker.Received(1).StrikeAndCheckLimit("hash1", Arg.Any<string>(), (ushort)3, StrikeType.DeadTorrent);
        await _striker.DidNotReceiveWithAnyArgs().ResetStrikeAsync(default!, default!, default);
    }

    [Fact]
    public async Task SkipsTorrentsNotInConfiguredCategories()
    {
        AddConfig(categories: ["movies"]);
        var downloads = new List<ITorrentItemWrapper> { CreateTorrent("hash1", "tv", 0) };

        await _sut.ProcessAsync(_downloadService, downloads);

        await _striker.DidNotReceiveWithAnyArgs().StrikeAndCheckLimit(default!, default!, default, default);
    }

    [Fact]
    public async Task SkipsTorrentsAlreadyInTargetCategory()
    {
        AddConfig();
        var downloads = new List<ITorrentItemWrapper> { CreateTorrent("hash1", "cleanuparr-dead", 0) };

        await _sut.ProcessAsync(_downloadService, downloads);

        await _striker.DidNotReceiveWithAnyArgs().StrikeAndCheckLimit(default!, default!, default, default);
    }
}
