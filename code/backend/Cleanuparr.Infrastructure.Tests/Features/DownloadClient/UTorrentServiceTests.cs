using Cleanuparr.Domain.Entities.UTorrent.Response;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;
using Moq;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient;

public class UTorrentServiceTests : IClassFixture<UTorrentServiceFixture>
{
    private readonly UTorrentServiceFixture _fixture;

    public UTorrentServiceTests(UTorrentServiceFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetMocks();
    }

    public class ShouldRemoveFromArrQueueAsync_BasicScenarios : UTorrentServiceTests
    {
        public ShouldRemoveFromArrQueueAsync_BasicScenarios(UTorrentServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task TorrentNotFound_ReturnsEmptyResult()
        {
            const string hash = "nonexistent";
            var sut = _fixture.CreateSut();

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentAsync(hash))
                .ReturnsAsync((UTorrentItem?)null);

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

            var torrentItem = new UTorrentItem
            {
                Hash = hash,
                Name = "Test Torrent",
                Status = 9, // Started + Checked = 1 + 8
                DownloadSpeed = 1000
            };

            var torrentProperties = new UTorrentProperties
            {
                Hash = hash,
                Pex = -1, // -1 means private torrent
                Trackers = ""
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentAsync(hash))
                .ReturnsAsync(torrentItem);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentPropertiesAsync(hash))
                .ReturnsAsync(torrentProperties);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFilesAsync(hash))
                .ReturnsAsync(new List<UTorrentFile>
                {
                    new UTorrentFile { Name = "file1.mkv", Priority = 1, Index = 0, Size = 1000, Downloaded = 500 }
                });

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateSlowRulesAsync(It.IsAny<UTorrentItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateStallRulesAsync(It.IsAny<UTorrentItemWrapper>()))
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

            var torrentItem = new UTorrentItem
            {
                Hash = hash,
                Name = "Test Torrent",
                Status = 9, // Started + Checked = 1 + 8
                DownloadSpeed = 1000
            };

            var torrentProperties = new UTorrentProperties
            {
                Hash = hash,
                Pex = 1, // 1 means public torrent (PEX enabled)
                Trackers = ""
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentAsync(hash))
                .ReturnsAsync(torrentItem);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentPropertiesAsync(hash))
                .ReturnsAsync(torrentProperties);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFilesAsync(hash))
                .ReturnsAsync(new List<UTorrentFile>
                {
                    new UTorrentFile { Name = "file1.mkv", Priority = 1, Index = 0, Size = 1000, Downloaded = 500 }
                });

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateSlowRulesAsync(It.IsAny<UTorrentItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateStallRulesAsync(It.IsAny<UTorrentItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            Assert.True(result.Found);
            Assert.False(result.IsPrivate);
        }
    }

    public class ShouldRemoveFromArrQueueAsync_AllFilesSkippedScenarios : UTorrentServiceTests
    {
        public ShouldRemoveFromArrQueueAsync_AllFilesSkippedScenarios(UTorrentServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task AllFilesUnwanted_DeletesFromClient()
        {
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            var torrentItem = new UTorrentItem
            {
                Hash = hash,
                Name = "Test Torrent",
                Status = 9,
                DownloadSpeed = 1000
            };

            var torrentProperties = new UTorrentProperties
            {
                Hash = hash,
                Pex = 1,
                Trackers = ""
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentAsync(hash))
                .ReturnsAsync(torrentItem);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentPropertiesAsync(hash))
                .ReturnsAsync(torrentProperties);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFilesAsync(hash))
                .ReturnsAsync(new List<UTorrentFile>
                {
                    new UTorrentFile { Name = "file1.mkv", Priority = 0, Index = 0, Size = 1000, Downloaded = 0 },
                    new UTorrentFile { Name = "file2.mkv", Priority = 0, Index = 1, Size = 2000, Downloaded = 0 }
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

            var torrentItem = new UTorrentItem
            {
                Hash = hash,
                Name = "Test Torrent",
                Status = 9,
                DownloadSpeed = 1000
            };

            var torrentProperties = new UTorrentProperties
            {
                Hash = hash,
                Pex = 1,
                Trackers = ""
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentAsync(hash))
                .ReturnsAsync(torrentItem);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentPropertiesAsync(hash))
                .ReturnsAsync(torrentProperties);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFilesAsync(hash))
                .ReturnsAsync(new List<UTorrentFile>
                {
                    new UTorrentFile { Name = "file1.mkv", Priority = 0, Index = 0, Size = 1000, Downloaded = 0 },
                    new UTorrentFile { Name = "file2.mkv", Priority = 1, Index = 1, Size = 2000, Downloaded = 1000 }
                });

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateSlowRulesAsync(It.IsAny<UTorrentItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateStallRulesAsync(It.IsAny<UTorrentItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            Assert.False(result.ShouldRemove);
        }
    }

    public class ShouldRemoveFromArrQueueAsync_IgnoredDownloadScenarios : UTorrentServiceTests
    {
        public ShouldRemoveFromArrQueueAsync_IgnoredDownloadScenarios(UTorrentServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task TorrentIgnoredByHash_ReturnsEmptyResult()
        {
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            var torrentItem = new UTorrentItem
            {
                Hash = hash,
                Name = "Test Torrent",
                Status = 9,
                DownloadSpeed = 1000
            };

            var torrentProperties = new UTorrentProperties
            {
                Hash = hash,
                Pex = 1,
                Trackers = ""
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentAsync(hash))
                .ReturnsAsync(torrentItem);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentPropertiesAsync(hash))
                .ReturnsAsync(torrentProperties);

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

            var torrentItem = new UTorrentItem
            {
                Hash = hash,
                Name = "Test Torrent",
                Status = 9,
                DownloadSpeed = 1000,
                Label = category
            };

            var torrentProperties = new UTorrentProperties
            {
                Hash = hash,
                Pex = 1,
                Trackers = ""
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentAsync(hash))
                .ReturnsAsync(torrentItem);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentPropertiesAsync(hash))
                .ReturnsAsync(torrentProperties);

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

            var torrentItem = new UTorrentItem
            {
                Hash = hash,
                Name = "Test Torrent",
                Status = 9,
                DownloadSpeed = 1000
            };

            var torrentProperties = new UTorrentProperties
            {
                Hash = hash,
                Pex = 1,
                Trackers = $"https://{trackerDomain}/announce\r\n"
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentAsync(hash))
                .ReturnsAsync(torrentItem);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentPropertiesAsync(hash))
                .ReturnsAsync(torrentProperties);

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, new[] { trackerDomain });

            Assert.True(result.Found);
            Assert.False(result.ShouldRemove);
        }
    }

    public class ShouldRemoveFromArrQueueAsync_ExceptionHandlingScenarios : UTorrentServiceTests
    {
        public ShouldRemoveFromArrQueueAsync_ExceptionHandlingScenarios(UTorrentServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task GetTorrentFilesAsync_ThrowsException_ContinuesProcessing()
        {
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            var torrentItem = new UTorrentItem
            {
                Hash = hash,
                Name = "Test Torrent",
                Status = 9,
                DownloadSpeed = 1000
            };

            var torrentProperties = new UTorrentProperties
            {
                Hash = hash,
                Pex = 1,
                Trackers = ""
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentAsync(hash))
                .ReturnsAsync(torrentItem);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentPropertiesAsync(hash))
                .ReturnsAsync(torrentProperties);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFilesAsync(hash))
                .ThrowsAsync(new InvalidOperationException("Failed to get files"));

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateSlowRulesAsync(It.IsAny<UTorrentItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateStallRulesAsync(It.IsAny<UTorrentItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            Assert.True(result.Found);
            Assert.False(result.ShouldRemove);
        }
    }

    public class ShouldRemoveFromArrQueueAsync_StateCheckScenarios : UTorrentServiceTests
    {
        public ShouldRemoveFromArrQueueAsync_StateCheckScenarios(UTorrentServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task NotDownloadingState_SkipsSlowCheck()
        {
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            var torrentItem = new UTorrentItem
            {
                Hash = hash,
                Name = "Test Torrent",
                Status = 32, // Paused
                DownloadSpeed = 0
            };

            var torrentProperties = new UTorrentProperties
            {
                Hash = hash,
                Pex = 1,
                Trackers = ""
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentAsync(hash))
                .ReturnsAsync(torrentItem);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentPropertiesAsync(hash))
                .ReturnsAsync(torrentProperties);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFilesAsync(hash))
                .ReturnsAsync(new List<UTorrentFile>
                {
                    new UTorrentFile { Name = "file1.mkv", Priority = 1, Index = 0, Size = 1000, Downloaded = 500 }
                });

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateStallRulesAsync(It.IsAny<UTorrentItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            Assert.False(result.ShouldRemove);
            _fixture.RuleEvaluator.Verify(x => x.EvaluateSlowRulesAsync(It.IsAny<UTorrentItemWrapper>()), Times.Never);
        }

        [Fact]
        public async Task ZeroDownloadSpeed_SkipsSlowCheck()
        {
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            var torrentItem = new UTorrentItem
            {
                Hash = hash,
                Name = "Test Torrent",
                Status = 9, // Started + Checked
                DownloadSpeed = 0
            };

            var torrentProperties = new UTorrentProperties
            {
                Hash = hash,
                Pex = 1,
                Trackers = ""
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentAsync(hash))
                .ReturnsAsync(torrentItem);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentPropertiesAsync(hash))
                .ReturnsAsync(torrentProperties);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFilesAsync(hash))
                .ReturnsAsync(new List<UTorrentFile>
                {
                    new UTorrentFile { Name = "file1.mkv", Priority = 1, Index = 0, Size = 1000, Downloaded = 500 }
                });

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateStallRulesAsync(It.IsAny<UTorrentItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            Assert.False(result.ShouldRemove);
            _fixture.RuleEvaluator.Verify(x => x.EvaluateSlowRulesAsync(It.IsAny<UTorrentItemWrapper>()), Times.Never);
        }
    }

    public class ShouldRemoveFromArrQueueAsync_SlowAndStalledScenarios : UTorrentServiceTests
    {
        public ShouldRemoveFromArrQueueAsync_SlowAndStalledScenarios(UTorrentServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task SlowDownload_MatchesRule_RemovesFromQueue()
        {
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            var torrentItem = new UTorrentItem
            {
                Hash = hash,
                Name = "Test Torrent",
                Status = 9, // Started + Checked
                DownloadSpeed = 1000
            };

            var torrentProperties = new UTorrentProperties
            {
                Hash = hash,
                Pex = 1,
                Trackers = ""
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentAsync(hash))
                .ReturnsAsync(torrentItem);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentPropertiesAsync(hash))
                .ReturnsAsync(torrentProperties);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFilesAsync(hash))
                .ReturnsAsync(new List<UTorrentFile>
                {
                    new UTorrentFile { Name = "file1.mkv", Priority = 1, Index = 0, Size = 1000, Downloaded = 500 }
                });

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateSlowRulesAsync(It.IsAny<UTorrentItemWrapper>()))
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

            var torrentItem = new UTorrentItem
            {
                Hash = hash,
                Name = "Test Torrent",
                Status = 9, // Started + Checked
                DownloadSpeed = 0,
                ETA = 0
            };

            var torrentProperties = new UTorrentProperties
            {
                Hash = hash,
                Pex = 1,
                Trackers = ""
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentAsync(hash))
                .ReturnsAsync(torrentItem);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentPropertiesAsync(hash))
                .ReturnsAsync(torrentProperties);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFilesAsync(hash))
                .ReturnsAsync(new List<UTorrentFile>
                {
                    new UTorrentFile { Name = "file1.mkv", Priority = 1, Index = 0, Size = 1000, Downloaded = 500 }
                });

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateStallRulesAsync(It.IsAny<UTorrentItemWrapper>()))
                .ReturnsAsync((true, DeleteReason.Stalled, true));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            Assert.True(result.ShouldRemove);
            Assert.Equal(DeleteReason.Stalled, result.DeleteReason);
            Assert.True(result.DeleteFromClient);
        }
    }
}
