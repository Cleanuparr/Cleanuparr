using Cleanuparr.Domain.Entities.RTorrent.Response;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient.RTorrent;
using Moq;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient;

public class RTorrentServiceTests : IClassFixture<RTorrentServiceFixture>
{
    private readonly RTorrentServiceFixture _fixture;

    public RTorrentServiceTests(RTorrentServiceFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetMocks();
    }

    public class ShouldRemoveFromArrQueueAsync_BasicScenarios : RTorrentServiceTests
    {
        public ShouldRemoveFromArrQueueAsync_BasicScenarios(RTorrentServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task TorrentNotFound_ReturnsEmptyResult()
        {
            // Arrange
            const string hash = "nonexistent";
            var sut = _fixture.CreateSut();

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentAsync(hash.ToUpperInvariant()))
                .ReturnsAsync((RTorrentTorrent?)null);

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            Assert.False(result.Found);
            Assert.False(result.ShouldRemove);
            Assert.Equal(DeleteReason.None, result.DeleteReason);
        }

        [Fact]
        public async Task TorrentWithEmptyHash_ReturnsEmptyResult()
        {
            // Arrange
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentAsync(hash.ToUpperInvariant()))
                .ReturnsAsync(new RTorrentTorrent { Hash = "", Name = "Test" });

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            Assert.False(result.Found);
            Assert.False(result.ShouldRemove);
        }

        [Fact]
        public async Task TorrentIsIgnored_ReturnsEmptyResult_WithFound()
        {
            // Arrange
            const string hash = "TEST-HASH";
            var sut = _fixture.CreateSut();

            var download = new RTorrentTorrent
            {
                Hash = hash,
                Name = "Test Torrent",
                IsPrivate = 0,
                Label = "ignored-category",
                State = 1,
                Complete = 0
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentAsync(hash))
                .ReturnsAsync(download);

            _fixture.ClientWrapper
                .Setup(x => x.GetTrackersAsync(hash))
                .ReturnsAsync(new List<string>());

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, new[] { "ignored-category" });

            // Assert
            Assert.True(result.Found);
            Assert.False(result.ShouldRemove);
        }

        [Fact]
        public async Task TorrentFound_SetsIsPrivateCorrectly_WhenPrivate()
        {
            // Arrange
            const string hash = "TEST-HASH";
            var sut = _fixture.CreateSut();

            var download = new RTorrentTorrent
            {
                Hash = hash,
                Name = "Test Torrent",
                IsPrivate = 1,
                State = 1,
                Complete = 0,
                DownRate = 1000,
                SizeBytes = 1000,
                CompletedBytes = 500
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentAsync(hash))
                .ReturnsAsync(download);

            _fixture.ClientWrapper
                .Setup(x => x.GetTrackersAsync(hash))
                .ReturnsAsync(new List<string>());

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFilesAsync(hash))
                .ReturnsAsync(new List<RTorrentFile>
                {
                    new RTorrentFile { Index = 0, Path = "file.mkv", Priority = 1 }
                });

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateSlowRulesAsync(It.IsAny<RTorrentItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateStallRulesAsync(It.IsAny<RTorrentItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            Assert.True(result.Found);
            Assert.True(result.IsPrivate);
        }

        [Fact]
        public async Task TorrentFound_SetsIsPrivateCorrectly_WhenPublic()
        {
            // Arrange
            const string hash = "TEST-HASH";
            var sut = _fixture.CreateSut();

            var download = new RTorrentTorrent
            {
                Hash = hash,
                Name = "Test Torrent",
                IsPrivate = 0,
                State = 1,
                Complete = 0,
                DownRate = 1000,
                SizeBytes = 1000,
                CompletedBytes = 500
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentAsync(hash))
                .ReturnsAsync(download);

            _fixture.ClientWrapper
                .Setup(x => x.GetTrackersAsync(hash))
                .ReturnsAsync(new List<string>());

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFilesAsync(hash))
                .ReturnsAsync(new List<RTorrentFile>
                {
                    new RTorrentFile { Index = 0, Path = "file.mkv", Priority = 1 }
                });

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateSlowRulesAsync(It.IsAny<RTorrentItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateStallRulesAsync(It.IsAny<RTorrentItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            Assert.True(result.Found);
            Assert.False(result.IsPrivate);
        }

        [Fact]
        public async Task NormalizesHashToUppercase()
        {
            // Arrange
            const string hash = "lowercase-hash";
            var sut = _fixture.CreateSut();

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentAsync("LOWERCASE-HASH"))
                .ReturnsAsync((RTorrentTorrent?)null);

            // Act
            await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            _fixture.ClientWrapper.Verify(
                x => x.GetTorrentAsync("LOWERCASE-HASH"),
                Times.Once);
        }
    }

    public class ShouldRemoveFromArrQueueAsync_AllFilesSkippedScenarios : RTorrentServiceTests
    {
        public ShouldRemoveFromArrQueueAsync_AllFilesSkippedScenarios(RTorrentServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task AllFilesSkipped_DeletesFromClient()
        {
            // Arrange
            const string hash = "TEST-HASH";
            var sut = _fixture.CreateSut();

            var download = new RTorrentTorrent
            {
                Hash = hash,
                Name = "Test Torrent",
                IsPrivate = 0,
                State = 1,
                Complete = 0
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentAsync(hash))
                .ReturnsAsync(download);

            _fixture.ClientWrapper
                .Setup(x => x.GetTrackersAsync(hash))
                .ReturnsAsync(new List<string>());

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFilesAsync(hash))
                .ReturnsAsync(new List<RTorrentFile>
                {
                    new RTorrentFile { Index = 0, Path = "file1.mkv", Priority = 0 },
                    new RTorrentFile { Index = 1, Path = "file2.mkv", Priority = 0 }
                });

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            Assert.True(result.ShouldRemove);
            Assert.Equal(DeleteReason.AllFilesSkipped, result.DeleteReason);
            Assert.True(result.DeleteFromClient);
        }

        [Fact]
        public async Task SomeFilesWanted_DoesNotRemove()
        {
            // Arrange
            const string hash = "TEST-HASH";
            var sut = _fixture.CreateSut();

            var download = new RTorrentTorrent
            {
                Hash = hash,
                Name = "Test Torrent",
                IsPrivate = 0,
                State = 1,
                Complete = 0,
                DownRate = 1000,
                SizeBytes = 1000,
                CompletedBytes = 500
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentAsync(hash))
                .ReturnsAsync(download);

            _fixture.ClientWrapper
                .Setup(x => x.GetTrackersAsync(hash))
                .ReturnsAsync(new List<string>());

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFilesAsync(hash))
                .ReturnsAsync(new List<RTorrentFile>
                {
                    new RTorrentFile { Index = 0, Path = "file1.mkv", Priority = 0 },
                    new RTorrentFile { Index = 1, Path = "file2.mkv", Priority = 1 } // At least one wanted
                });

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateSlowRulesAsync(It.IsAny<RTorrentItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateStallRulesAsync(It.IsAny<RTorrentItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            Assert.False(result.ShouldRemove);
        }
    }

    public class ShouldRemoveFromArrQueueAsync_FileErrorScenarios : RTorrentServiceTests
    {
        public ShouldRemoveFromArrQueueAsync_FileErrorScenarios(RTorrentServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task GetTorrentFilesThrows_ReturnsEmptyResult()
        {
            // Arrange
            const string hash = "TEST-HASH";
            var sut = _fixture.CreateSut();

            var download = new RTorrentTorrent
            {
                Hash = hash,
                Name = "Test Torrent",
                IsPrivate = 0,
                State = 1,
                Complete = 0
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentAsync(hash))
                .ReturnsAsync(download);

            _fixture.ClientWrapper
                .Setup(x => x.GetTrackersAsync(hash))
                .ReturnsAsync(new List<string>());

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFilesAsync(hash))
                .ThrowsAsync(new Exception("XML-RPC error"));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            Assert.True(result.Found);
            Assert.False(result.ShouldRemove);
            Assert.Equal(DeleteReason.None, result.DeleteReason);
        }
    }

    public class ShouldRemoveFromArrQueueAsync_SlowDownloadScenarios : RTorrentServiceTests
    {
        public ShouldRemoveFromArrQueueAsync_SlowDownloadScenarios(RTorrentServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task SlowDownload_NotInDownloadingState_SkipsCheck()
        {
            // Arrange
            const string hash = "TEST-HASH";
            var sut = _fixture.CreateSut();

            // State=1, Complete=1 means seeding (not downloading)
            var download = new RTorrentTorrent
            {
                Hash = hash,
                Name = "Test Torrent",
                IsPrivate = 0,
                State = 1,
                Complete = 1,
                DownRate = 100
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentAsync(hash))
                .ReturnsAsync(download);

            _fixture.ClientWrapper
                .Setup(x => x.GetTrackersAsync(hash))
                .ReturnsAsync(new List<string>());

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFilesAsync(hash))
                .ReturnsAsync(new List<RTorrentFile>
                {
                    new RTorrentFile { Index = 0, Path = "file.mkv", Priority = 1 }
                });

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateStallRulesAsync(It.IsAny<RTorrentItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            Assert.False(result.ShouldRemove);
            _fixture.RuleEvaluator.Verify(
                x => x.EvaluateSlowRulesAsync(It.IsAny<RTorrentItemWrapper>()),
                Times.Never);
        }

        [Fact]
        public async Task SlowDownload_ZeroSpeed_SkipsCheck()
        {
            // Arrange
            const string hash = "TEST-HASH";
            var sut = _fixture.CreateSut();

            // State=1, Complete=0 means downloading; DownRate=0 means zero speed
            var download = new RTorrentTorrent
            {
                Hash = hash,
                Name = "Test Torrent",
                IsPrivate = 0,
                State = 1,
                Complete = 0,
                DownRate = 0,
                SizeBytes = 1000,
                CompletedBytes = 500
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentAsync(hash))
                .ReturnsAsync(download);

            _fixture.ClientWrapper
                .Setup(x => x.GetTrackersAsync(hash))
                .ReturnsAsync(new List<string>());

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFilesAsync(hash))
                .ReturnsAsync(new List<RTorrentFile>
                {
                    new RTorrentFile { Index = 0, Path = "file.mkv", Priority = 1 }
                });

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateStallRulesAsync(It.IsAny<RTorrentItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            Assert.False(result.ShouldRemove);
            _fixture.RuleEvaluator.Verify(
                x => x.EvaluateSlowRulesAsync(It.IsAny<RTorrentItemWrapper>()),
                Times.Never);
        }

        [Fact]
        public async Task SlowDownload_MatchesRule_RemovesFromQueue()
        {
            // Arrange
            const string hash = "TEST-HASH";
            var sut = _fixture.CreateSut();

            // State=1, Complete=0 means downloading; DownRate > 0 means some speed
            var download = new RTorrentTorrent
            {
                Hash = hash,
                Name = "Test Torrent",
                IsPrivate = 0,
                State = 1,
                Complete = 0,
                DownRate = 1000,
                SizeBytes = 1000,
                CompletedBytes = 500
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentAsync(hash))
                .ReturnsAsync(download);

            _fixture.ClientWrapper
                .Setup(x => x.GetTrackersAsync(hash))
                .ReturnsAsync(new List<string>());

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFilesAsync(hash))
                .ReturnsAsync(new List<RTorrentFile>
                {
                    new RTorrentFile { Index = 0, Path = "file.mkv", Priority = 1 }
                });

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateSlowRulesAsync(It.IsAny<RTorrentItemWrapper>()))
                .ReturnsAsync((true, DeleteReason.SlowSpeed, true));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            Assert.True(result.ShouldRemove);
            Assert.Equal(DeleteReason.SlowSpeed, result.DeleteReason);
            Assert.True(result.DeleteFromClient);
        }
    }

    public class ShouldRemoveFromArrQueueAsync_StalledDownloadScenarios : RTorrentServiceTests
    {
        public ShouldRemoveFromArrQueueAsync_StalledDownloadScenarios(RTorrentServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task StalledDownload_NotInStalledState_SkipsCheck()
        {
            // Arrange
            const string hash = "TEST-HASH";
            var sut = _fixture.CreateSut();

            // State=1, Complete=0, DownRate > 0 = downloading with speed (not stalled)
            var download = new RTorrentTorrent
            {
                Hash = hash,
                Name = "Test Torrent",
                IsPrivate = 0,
                State = 1,
                Complete = 0,
                DownRate = 5000,
                SizeBytes = 1000,
                CompletedBytes = 500
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentAsync(hash))
                .ReturnsAsync(download);

            _fixture.ClientWrapper
                .Setup(x => x.GetTrackersAsync(hash))
                .ReturnsAsync(new List<string>());

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFilesAsync(hash))
                .ReturnsAsync(new List<RTorrentFile>
                {
                    new RTorrentFile { Index = 0, Path = "file.mkv", Priority = 1 }
                });

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateSlowRulesAsync(It.IsAny<RTorrentItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            Assert.False(result.ShouldRemove);
            _fixture.RuleEvaluator.Verify(
                x => x.EvaluateStallRulesAsync(It.IsAny<RTorrentItemWrapper>()),
                Times.Never);
        }

        [Fact]
        public async Task StalledDownload_MatchesRule_RemovesFromQueue()
        {
            // Arrange
            const string hash = "TEST-HASH";
            var sut = _fixture.CreateSut();

            // State=1, Complete=0, DownRate=0 = stalled (downloading with no speed)
            var download = new RTorrentTorrent
            {
                Hash = hash,
                Name = "Test Torrent",
                IsPrivate = 0,
                State = 1,
                Complete = 0,
                DownRate = 0,
                SizeBytes = 1000,
                CompletedBytes = 500
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentAsync(hash))
                .ReturnsAsync(download);

            _fixture.ClientWrapper
                .Setup(x => x.GetTrackersAsync(hash))
                .ReturnsAsync(new List<string>());

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFilesAsync(hash))
                .ReturnsAsync(new List<RTorrentFile>
                {
                    new RTorrentFile { Index = 0, Path = "file.mkv", Priority = 1 }
                });

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateStallRulesAsync(It.IsAny<RTorrentItemWrapper>()))
                .ReturnsAsync((true, DeleteReason.Stalled, true));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            Assert.True(result.ShouldRemove);
            Assert.Equal(DeleteReason.Stalled, result.DeleteReason);
            Assert.True(result.DeleteFromClient);
        }
    }

    public class ShouldRemoveFromArrQueueAsync_IntegrationScenarios : RTorrentServiceTests
    {
        public ShouldRemoveFromArrQueueAsync_IntegrationScenarios(RTorrentServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task SlowCheckPasses_ButStalledCheckFails_RemovesFromQueue()
        {
            // Arrange
            const string hash = "TEST-HASH";
            var sut = _fixture.CreateSut();

            // State=1, Complete=0, DownRate=0 = stalled (not downloading, so slow check skipped)
            var download = new RTorrentTorrent
            {
                Hash = hash,
                Name = "Test Torrent",
                IsPrivate = 0,
                State = 1,
                Complete = 0,
                DownRate = 0,
                SizeBytes = 1000,
                CompletedBytes = 500
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentAsync(hash))
                .ReturnsAsync(download);

            _fixture.ClientWrapper
                .Setup(x => x.GetTrackersAsync(hash))
                .ReturnsAsync(new List<string>());

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFilesAsync(hash))
                .ReturnsAsync(new List<RTorrentFile>
                {
                    new RTorrentFile { Index = 0, Path = "file.mkv", Priority = 1 }
                });

            // Slow check is skipped because speed is 0
            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateStallRulesAsync(It.IsAny<RTorrentItemWrapper>()))
                .ReturnsAsync((true, DeleteReason.Stalled, true));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            Assert.True(result.ShouldRemove);
            Assert.Equal(DeleteReason.Stalled, result.DeleteReason);
            _fixture.RuleEvaluator.Verify(
                x => x.EvaluateSlowRulesAsync(It.IsAny<RTorrentItemWrapper>()),
                Times.Never); // Skipped
            _fixture.RuleEvaluator.Verify(
                x => x.EvaluateStallRulesAsync(It.IsAny<RTorrentItemWrapper>()),
                Times.Once);
        }

        [Fact]
        public async Task BothChecksPass_DoesNotRemove()
        {
            // Arrange
            const string hash = "TEST-HASH";
            var sut = _fixture.CreateSut();

            var download = new RTorrentTorrent
            {
                Hash = hash,
                Name = "Test Torrent",
                IsPrivate = 0,
                State = 1,
                Complete = 0,
                DownRate = 5000000, // Good speed
                SizeBytes = 10000000,
                CompletedBytes = 5000000
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentAsync(hash))
                .ReturnsAsync(download);

            _fixture.ClientWrapper
                .Setup(x => x.GetTrackersAsync(hash))
                .ReturnsAsync(new List<string>());

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFilesAsync(hash))
                .ReturnsAsync(new List<RTorrentFile>
                {
                    new RTorrentFile { Index = 0, Path = "file.mkv", Priority = 1 }
                });

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateSlowRulesAsync(It.IsAny<RTorrentItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateStallRulesAsync(It.IsAny<RTorrentItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            Assert.False(result.ShouldRemove);
            Assert.Equal(DeleteReason.None, result.DeleteReason);
        }
    }
}
