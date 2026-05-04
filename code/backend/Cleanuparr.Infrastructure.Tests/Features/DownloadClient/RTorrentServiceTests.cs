using Cleanuparr.Domain.Entities.RTorrent.Response;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient.RTorrent;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
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
                .GetTorrentAsync(hash.ToUpperInvariant())
                .Returns((RTorrentTorrent?)null);

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            result.Found.ShouldBeFalse();
            result.ShouldRemove.ShouldBeFalse();
            result.DeleteReason.ShouldBe(DeleteReason.None);
        }

        [Fact]
        public async Task TorrentWithEmptyHash_ReturnsEmptyResult()
        {
            // Arrange
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            _fixture.ClientWrapper
                .GetTorrentAsync(hash.ToUpperInvariant())
                .Returns(new RTorrentTorrent { Hash = "", Name = "Test" });

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            result.Found.ShouldBeFalse();
            result.ShouldRemove.ShouldBeFalse();
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
                .GetTorrentAsync(hash)
                .Returns(download);

            _fixture.ClientWrapper
                .GetTrackersAsync(hash)
                .Returns(new List<string>());

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, new[] { "ignored-category" });

            // Assert
            result.Found.ShouldBeTrue();
            result.ShouldRemove.ShouldBeFalse();
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
                .GetTorrentAsync(hash)
                .Returns(download);

            _fixture.ClientWrapper
                .GetTrackersAsync(hash)
                .Returns(new List<string>());

            _fixture.ClientWrapper
                .GetTorrentFilesAsync(hash)
                .Returns(new List<RTorrentFile>
                {
                    new RTorrentFile { Index = 0, Path = "file.mkv", Priority = 1 }
                });

            _fixture.RuleEvaluator
                .EvaluateSlowRulesAsync(Arg.Any<RTorrentItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<RTorrentItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            result.Found.ShouldBeTrue();
            result.IsPrivate.ShouldBeTrue();
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
                .GetTorrentAsync(hash)
                .Returns(download);

            _fixture.ClientWrapper
                .GetTrackersAsync(hash)
                .Returns(new List<string>());

            _fixture.ClientWrapper
                .GetTorrentFilesAsync(hash)
                .Returns(new List<RTorrentFile>
                {
                    new RTorrentFile { Index = 0, Path = "file.mkv", Priority = 1 }
                });

            _fixture.RuleEvaluator
                .EvaluateSlowRulesAsync(Arg.Any<RTorrentItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<RTorrentItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            result.Found.ShouldBeTrue();
            result.IsPrivate.ShouldBeFalse();
        }

        [Fact]
        public async Task NormalizesHashToUppercase()
        {
            // Arrange
            const string hash = "lowercase-hash";
            var sut = _fixture.CreateSut();

            _fixture.ClientWrapper
                .GetTorrentAsync("LOWERCASE-HASH")
                .Returns((RTorrentTorrent?)null);

            // Act
            await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            await _fixture.ClientWrapper.Received(1)
                .GetTorrentAsync("LOWERCASE-HASH");
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
                .GetTorrentAsync(hash)
                .Returns(download);

            _fixture.ClientWrapper
                .GetTrackersAsync(hash)
                .Returns(new List<string>());

            _fixture.ClientWrapper
                .GetTorrentFilesAsync(hash)
                .Returns(new List<RTorrentFile>
                {
                    new RTorrentFile { Index = 0, Path = "file1.mkv", Priority = 0 },
                    new RTorrentFile { Index = 1, Path = "file2.mkv", Priority = 0 }
                });

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            result.ShouldRemove.ShouldBeTrue();
            result.DeleteReason.ShouldBe(DeleteReason.AllFilesSkipped);
            result.DeleteFromClient.ShouldBeTrue();
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
                .GetTorrentAsync(hash)
                .Returns(download);

            _fixture.ClientWrapper
                .GetTrackersAsync(hash)
                .Returns(new List<string>());

            _fixture.ClientWrapper
                .GetTorrentFilesAsync(hash)
                .Returns(new List<RTorrentFile>
                {
                    new RTorrentFile { Index = 0, Path = "file1.mkv", Priority = 0 },
                    new RTorrentFile { Index = 1, Path = "file2.mkv", Priority = 1 } // At least one wanted
                });

            _fixture.RuleEvaluator
                .EvaluateSlowRulesAsync(Arg.Any<RTorrentItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<RTorrentItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            result.ShouldRemove.ShouldBeFalse();
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
                .GetTorrentAsync(hash)
                .Returns(download);

            _fixture.ClientWrapper
                .GetTrackersAsync(hash)
                .Returns(new List<string>());

            _fixture.ClientWrapper
                .GetTorrentFilesAsync(hash)
                .Throws(new Exception("XML-RPC error"));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            result.Found.ShouldBeTrue();
            result.ShouldRemove.ShouldBeFalse();
            result.DeleteReason.ShouldBe(DeleteReason.None);
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
                .GetTorrentAsync(hash)
                .Returns(download);

            _fixture.ClientWrapper
                .GetTrackersAsync(hash)
                .Returns(new List<string>());

            _fixture.ClientWrapper
                .GetTorrentFilesAsync(hash)
                .Returns(new List<RTorrentFile>
                {
                    new RTorrentFile { Index = 0, Path = "file.mkv", Priority = 1 }
                });

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<RTorrentItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            result.ShouldRemove.ShouldBeFalse();
            await _fixture.RuleEvaluator.DidNotReceive()
                .EvaluateSlowRulesAsync(Arg.Any<RTorrentItemWrapper>());
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
                .GetTorrentAsync(hash)
                .Returns(download);

            _fixture.ClientWrapper
                .GetTrackersAsync(hash)
                .Returns(new List<string>());

            _fixture.ClientWrapper
                .GetTorrentFilesAsync(hash)
                .Returns(new List<RTorrentFile>
                {
                    new RTorrentFile { Index = 0, Path = "file.mkv", Priority = 1 }
                });

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<RTorrentItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            result.ShouldRemove.ShouldBeFalse();
            await _fixture.RuleEvaluator.DidNotReceive()
                .EvaluateSlowRulesAsync(Arg.Any<RTorrentItemWrapper>());
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
                .GetTorrentAsync(hash)
                .Returns(download);

            _fixture.ClientWrapper
                .GetTrackersAsync(hash)
                .Returns(new List<string>());

            _fixture.ClientWrapper
                .GetTorrentFilesAsync(hash)
                .Returns(new List<RTorrentFile>
                {
                    new RTorrentFile { Index = 0, Path = "file.mkv", Priority = 1 }
                });

            _fixture.RuleEvaluator
                .EvaluateSlowRulesAsync(Arg.Any<RTorrentItemWrapper>())
                .Returns((true, DeleteReason.SlowSpeed, true, false));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            result.ShouldRemove.ShouldBeTrue();
            result.DeleteReason.ShouldBe(DeleteReason.SlowSpeed);
            result.DeleteFromClient.ShouldBeTrue();
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
                .GetTorrentAsync(hash)
                .Returns(download);

            _fixture.ClientWrapper
                .GetTrackersAsync(hash)
                .Returns(new List<string>());

            _fixture.ClientWrapper
                .GetTorrentFilesAsync(hash)
                .Returns(new List<RTorrentFile>
                {
                    new RTorrentFile { Index = 0, Path = "file.mkv", Priority = 1 }
                });

            _fixture.RuleEvaluator
                .EvaluateSlowRulesAsync(Arg.Any<RTorrentItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            result.ShouldRemove.ShouldBeFalse();
            await _fixture.RuleEvaluator.DidNotReceive()
                .EvaluateStallRulesAsync(Arg.Any<RTorrentItemWrapper>());
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
                .GetTorrentAsync(hash)
                .Returns(download);

            _fixture.ClientWrapper
                .GetTrackersAsync(hash)
                .Returns(new List<string>());

            _fixture.ClientWrapper
                .GetTorrentFilesAsync(hash)
                .Returns(new List<RTorrentFile>
                {
                    new RTorrentFile { Index = 0, Path = "file.mkv", Priority = 1 }
                });

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<RTorrentItemWrapper>())
                .Returns((true, DeleteReason.Stalled, true, false));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            result.ShouldRemove.ShouldBeTrue();
            result.DeleteReason.ShouldBe(DeleteReason.Stalled);
            result.DeleteFromClient.ShouldBeTrue();
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
                .GetTorrentAsync(hash)
                .Returns(download);

            _fixture.ClientWrapper
                .GetTrackersAsync(hash)
                .Returns(new List<string>());

            _fixture.ClientWrapper
                .GetTorrentFilesAsync(hash)
                .Returns(new List<RTorrentFile>
                {
                    new RTorrentFile { Index = 0, Path = "file.mkv", Priority = 1 }
                });

            // Slow check is skipped because speed is 0
            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<RTorrentItemWrapper>())
                .Returns((true, DeleteReason.Stalled, true, false));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            result.ShouldRemove.ShouldBeTrue();
            result.DeleteReason.ShouldBe(DeleteReason.Stalled);
            await _fixture.RuleEvaluator.DidNotReceive()
                .EvaluateSlowRulesAsync(Arg.Any<RTorrentItemWrapper>()); // Skipped
            await _fixture.RuleEvaluator.Received(1)
                .EvaluateStallRulesAsync(Arg.Any<RTorrentItemWrapper>());
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
                .GetTorrentAsync(hash)
                .Returns(download);

            _fixture.ClientWrapper
                .GetTrackersAsync(hash)
                .Returns(new List<string>());

            _fixture.ClientWrapper
                .GetTorrentFilesAsync(hash)
                .Returns(new List<RTorrentFile>
                {
                    new RTorrentFile { Index = 0, Path = "file.mkv", Priority = 1 }
                });

            _fixture.RuleEvaluator
                .EvaluateSlowRulesAsync(Arg.Any<RTorrentItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<RTorrentItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            result.ShouldRemove.ShouldBeFalse();
            result.DeleteReason.ShouldBe(DeleteReason.None);
        }
    }
}
