using Cleanuparr.Domain.Entities.Deluge.Response;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.DownloadClient.Deluge;
using Moq;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient;

public class DelugeServiceTests : IClassFixture<DelugeServiceFixture>
{
    private readonly DelugeServiceFixture _fixture;

    public DelugeServiceTests(DelugeServiceFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetMocks();
    }

    public class ShouldRemoveFromArrQueueAsync_BasicScenarios : DelugeServiceTests
    {
        public ShouldRemoveFromArrQueueAsync_BasicScenarios(DelugeServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task TorrentNotFound_ReturnsEmptyResult()
        {
            const string hash = "nonexistent";
            var sut = _fixture.CreateSut();

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentStatus(hash))
                .ReturnsAsync((DownloadStatus?)null);

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            Assert.False(result.Found);
            Assert.False(result.ShouldRemove);
            Assert.Equal(DeleteReason.None, result.DeleteReason);
        }

        [Fact]
        public async Task TorrentFound_SetsIsPrivateCorrectly_WhenPrivate()
        {
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            var downloadStatus = new DownloadStatus
            {
                Hash = hash,
                Name = "Test Torrent",
                State = "Downloading",
                Private = true,
                DownloadSpeed = 1000,
                Trackers = new List<Tracker>(),
                DownloadLocation = "/downloads"
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentStatus(hash))
                .ReturnsAsync(downloadStatus);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFiles(hash))
                .ReturnsAsync(new DelugeContents
                {
                    Contents = new Dictionary<string, DelugeFileOrDirectory>
                    {
                        { "file1.mkv", new DelugeFileOrDirectory { Type = "file", Priority = 1, Index = 0 } }
                    }
                });

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateSlowRulesAsync(It.IsAny<DelugeItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateStallRulesAsync(It.IsAny<DelugeItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            Assert.True(result.Found);
            Assert.True(result.IsPrivate);
        }

        [Fact]
        public async Task TorrentFound_SetsIsPrivateCorrectly_WhenPublic()
        {
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            var downloadStatus = new DownloadStatus
            {
                Hash = hash,
                Name = "Test Torrent",
                State = "Downloading",
                Private = false,
                DownloadSpeed = 1000,
                Trackers = new List<Tracker>(),
                DownloadLocation = "/downloads"
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentStatus(hash))
                .ReturnsAsync(downloadStatus);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFiles(hash))
                .ReturnsAsync(new DelugeContents
                {
                    Contents = new Dictionary<string, DelugeFileOrDirectory>
                    {
                        { "file1.mkv", new DelugeFileOrDirectory { Type = "file", Priority = 1, Index = 0 } }
                    }
                });

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateSlowRulesAsync(It.IsAny<DelugeItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateStallRulesAsync(It.IsAny<DelugeItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            Assert.True(result.Found);
            Assert.False(result.IsPrivate);
        }
    }

    public class ShouldRemoveFromArrQueueAsync_AllFilesSkippedScenarios : DelugeServiceTests
    {
        public ShouldRemoveFromArrQueueAsync_AllFilesSkippedScenarios(DelugeServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task AllFilesUnwanted_DeletesFromClient()
        {
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            var downloadStatus = new DownloadStatus
            {
                Hash = hash,
                Name = "Test Torrent",
                State = "Downloading",
                Private = false,
                DownloadSpeed = 1000,
                Trackers = new List<Tracker>(),
                DownloadLocation = "/downloads"
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentStatus(hash))
                .ReturnsAsync(downloadStatus);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFiles(hash))
                .ReturnsAsync(new DelugeContents
                {
                    Contents = new Dictionary<string, DelugeFileOrDirectory>
                    {
                        { "file1.mkv", new DelugeFileOrDirectory { Type = "file", Priority = 0, Index = 0 } },
                        { "file2.mkv", new DelugeFileOrDirectory { Type = "file", Priority = 0, Index = 1 } }
                    }
                });

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            Assert.True(result.ShouldRemove);
            Assert.Equal(DeleteReason.AllFilesSkipped, result.DeleteReason);
            Assert.True(result.DeleteFromClient);
        }

        [Fact]
        public async Task SomeFilesWanted_DoesNotRemove()
        {
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            var downloadStatus = new DownloadStatus
            {
                Hash = hash,
                Name = "Test Torrent",
                State = "Downloading",
                Private = false,
                DownloadSpeed = 1000,
                Trackers = new List<Tracker>(),
                DownloadLocation = "/downloads"
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentStatus(hash))
                .ReturnsAsync(downloadStatus);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFiles(hash))
                .ReturnsAsync(new DelugeContents
                {
                    Contents = new Dictionary<string, DelugeFileOrDirectory>
                    {
                        { "file1.mkv", new DelugeFileOrDirectory { Type = "file", Priority = 0, Index = 0 } },
                        { "file2.mkv", new DelugeFileOrDirectory { Type = "file", Priority = 1, Index = 1 } }
                    }
                });

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateSlowRulesAsync(It.IsAny<DelugeItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateStallRulesAsync(It.IsAny<DelugeItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            Assert.False(result.ShouldRemove);
        }
    }

    public class ShouldRemoveFromArrQueueAsync_IgnoredDownloadScenarios : DelugeServiceTests
    {
        public ShouldRemoveFromArrQueueAsync_IgnoredDownloadScenarios(DelugeServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task TorrentIgnoredByHash_ReturnsEmptyResult()
        {
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            var downloadStatus = new DownloadStatus
            {
                Hash = hash,
                Name = "Test Torrent",
                State = "Downloading",
                Private = false,
                DownloadSpeed = 1000,
                Trackers = new List<Tracker>(),
                DownloadLocation = "/downloads"
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentStatus(hash))
                .ReturnsAsync(downloadStatus);

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, new[] { hash });

            Assert.True(result.Found);
            Assert.False(result.ShouldRemove);
        }

        [Fact]
        public async Task TorrentIgnoredByCategory_ReturnsEmptyResult()
        {
            const string hash = "test-hash";
            const string category = "test-category";
            var sut = _fixture.CreateSut();

            var downloadStatus = new DownloadStatus
            {
                Hash = hash,
                Name = "Test Torrent",
                State = "Downloading",
                Private = false,
                DownloadSpeed = 1000,
                Label = category,
                Trackers = new List<Tracker>(),
                DownloadLocation = "/downloads"
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentStatus(hash))
                .ReturnsAsync(downloadStatus);

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, new[] { category });

            Assert.True(result.Found);
            Assert.False(result.ShouldRemove);
        }

        [Fact]
        public async Task TorrentIgnoredByTrackerDomain_ReturnsEmptyResult()
        {
            const string hash = "test-hash";
            const string trackerDomain = "tracker.example.com";
            var sut = _fixture.CreateSut();

            var downloadStatus = new DownloadStatus
            {
                Hash = hash,
                Name = "Test Torrent",
                State = "Downloading",
                Private = false,
                DownloadSpeed = 1000,
                Trackers = new List<Tracker>
                {
                    new Tracker { Url = $"https://{trackerDomain}/announce" }
                },
                DownloadLocation = "/downloads"
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentStatus(hash))
                .ReturnsAsync(downloadStatus);

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, new[] { trackerDomain });

            Assert.True(result.Found);
            Assert.False(result.ShouldRemove);
        }
    }

    public class ShouldRemoveFromArrQueueAsync_StateCheckScenarios : DelugeServiceTests
    {
        public ShouldRemoveFromArrQueueAsync_StateCheckScenarios(DelugeServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task NotDownloadingState_SkipsSlowCheck()
        {
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            var downloadStatus = new DownloadStatus
            {
                Hash = hash,
                Name = "Test Torrent",
                State = "Seeding",
                Private = false,
                DownloadSpeed = 0,
                Trackers = new List<Tracker>(),
                DownloadLocation = "/downloads"
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentStatus(hash))
                .ReturnsAsync(downloadStatus);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFiles(hash))
                .ReturnsAsync(new DelugeContents
                {
                    Contents = new Dictionary<string, DelugeFileOrDirectory>
                    {
                        { "file1.mkv", new DelugeFileOrDirectory { Type = "file", Priority = 1, Index = 0 } }
                    }
                });

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateStallRulesAsync(It.IsAny<DelugeItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            Assert.False(result.ShouldRemove);
            _fixture.RuleEvaluator.Verify(x => x.EvaluateSlowRulesAsync(It.IsAny<DelugeItemWrapper>()), Times.Never);
        }

        [Fact]
        public async Task ZeroDownloadSpeed_SkipsSlowCheck()
        {
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            var downloadStatus = new DownloadStatus
            {
                Hash = hash,
                Name = "Test Torrent",
                State = "Downloading",
                Private = false,
                DownloadSpeed = 0,
                Trackers = new List<Tracker>(),
                DownloadLocation = "/downloads"
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentStatus(hash))
                .ReturnsAsync(downloadStatus);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFiles(hash))
                .ReturnsAsync(new DelugeContents
                {
                    Contents = new Dictionary<string, DelugeFileOrDirectory>
                    {
                        { "file1.mkv", new DelugeFileOrDirectory { Type = "file", Priority = 1, Index = 0 } }
                    }
                });

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateStallRulesAsync(It.IsAny<DelugeItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            Assert.False(result.ShouldRemove);
            _fixture.RuleEvaluator.Verify(x => x.EvaluateSlowRulesAsync(It.IsAny<DelugeItemWrapper>()), Times.Never);
        }
    }

    public class ShouldRemoveFromArrQueueAsync_SlowAndStalledScenarios : DelugeServiceTests
    {
        public ShouldRemoveFromArrQueueAsync_SlowAndStalledScenarios(DelugeServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task SlowDownload_MatchesRule_RemovesFromQueue()
        {
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            var downloadStatus = new DownloadStatus
            {
                Hash = hash,
                Name = "Test Torrent",
                State = "Downloading",
                Private = false,
                DownloadSpeed = 1000,
                Trackers = new List<Tracker>(),
                DownloadLocation = "/downloads"
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentStatus(hash))
                .ReturnsAsync(downloadStatus);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFiles(hash))
                .ReturnsAsync(new DelugeContents
                {
                    Contents = new Dictionary<string, DelugeFileOrDirectory>
                    {
                        { "file1.mkv", new DelugeFileOrDirectory { Type = "file", Priority = 1, Index = 0 } }
                    }
                });

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateSlowRulesAsync(It.IsAny<DelugeItemWrapper>()))
                .ReturnsAsync((true, DeleteReason.SlowSpeed, true));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            Assert.True(result.ShouldRemove);
            Assert.Equal(DeleteReason.SlowSpeed, result.DeleteReason);
            Assert.True(result.DeleteFromClient);
        }

        [Fact]
        public async Task StalledDownload_MatchesRule_RemovesFromQueue()
        {
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            var downloadStatus = new DownloadStatus
            {
                Hash = hash,
                Name = "Test Torrent",
                State = "Downloading",
                DownloadSpeed = 0,
                Eta = 0,
                Private = false,
                Trackers = new List<Tracker>(),
                DownloadLocation = "/downloads"
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentStatus(hash))
                .ReturnsAsync(downloadStatus);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFiles(hash))
                .ReturnsAsync(new DelugeContents
                {
                    Contents = new Dictionary<string, DelugeFileOrDirectory>
                    {
                        { "file1.mkv", new DelugeFileOrDirectory { Type = "file", Priority = 1, Index = 0 } }
                    }
                });

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateStallRulesAsync(It.IsAny<DelugeItemWrapper>()))
                .ReturnsAsync((true, DeleteReason.Stalled, true));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            Assert.True(result.ShouldRemove);
            Assert.Equal(DeleteReason.Stalled, result.DeleteReason);
            Assert.True(result.DeleteFromClient);
        }
    }
}
