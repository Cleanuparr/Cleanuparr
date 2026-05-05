using Cleanuparr.Domain.Entities.UTorrent.Response;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
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
                .GetTorrentAsync(hash)
                .Returns((UTorrentItem?)null);

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            result.Found.ShouldBeFalse();
            result.ShouldRemove.ShouldBeFalse();
            result.DeleteReason.ShouldBe(DeleteReason.None);
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
                .GetTorrentAsync(hash)
                .Returns(torrentItem);

            _fixture.ClientWrapper
                .GetTorrentPropertiesAsync(hash)
                .Returns(torrentProperties);

            _fixture.ClientWrapper
                .GetTorrentFilesAsync(hash)
                .Returns(new List<UTorrentFile>
                {
                    new UTorrentFile { Name = "file1.mkv", Priority = 1, Index = 0, Size = 1000, Downloaded = 500 }
                });

            _fixture.RuleEvaluator
                .EvaluateSlowRulesAsync(Arg.Any<UTorrentItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<UTorrentItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            result.Found.ShouldBeTrue();
            result.IsPrivate.ShouldBeTrue();
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
                .GetTorrentAsync(hash)
                .Returns(torrentItem);

            _fixture.ClientWrapper
                .GetTorrentPropertiesAsync(hash)
                .Returns(torrentProperties);

            _fixture.ClientWrapper
                .GetTorrentFilesAsync(hash)
                .Returns(new List<UTorrentFile>
                {
                    new UTorrentFile { Name = "file1.mkv", Priority = 1, Index = 0, Size = 1000, Downloaded = 500 }
                });

            _fixture.RuleEvaluator
                .EvaluateSlowRulesAsync(Arg.Any<UTorrentItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<UTorrentItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            result.Found.ShouldBeTrue();
            result.IsPrivate.ShouldBeFalse();
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
                .GetTorrentAsync(hash)
                .Returns(torrentItem);

            _fixture.ClientWrapper
                .GetTorrentPropertiesAsync(hash)
                .Returns(torrentProperties);

            _fixture.ClientWrapper
                .GetTorrentFilesAsync(hash)
                .Returns(new List<UTorrentFile>
                {
                    new UTorrentFile { Name = "file1.mkv", Priority = 0, Index = 0, Size = 1000, Downloaded = 0 },
                    new UTorrentFile { Name = "file2.mkv", Priority = 0, Index = 1, Size = 2000, Downloaded = 0 }
                });

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            result.ShouldRemove.ShouldBeTrue();
            result.DeleteReason.ShouldBe(DeleteReason.AllFilesSkipped);
            result.DeleteFromClient.ShouldBeTrue();
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
                .GetTorrentAsync(hash)
                .Returns(torrentItem);

            _fixture.ClientWrapper
                .GetTorrentPropertiesAsync(hash)
                .Returns(torrentProperties);

            _fixture.ClientWrapper
                .GetTorrentFilesAsync(hash)
                .Returns(new List<UTorrentFile>
                {
                    new UTorrentFile { Name = "file1.mkv", Priority = 0, Index = 0, Size = 1000, Downloaded = 0 },
                    new UTorrentFile { Name = "file2.mkv", Priority = 1, Index = 1, Size = 2000, Downloaded = 1000 }
                });

            _fixture.RuleEvaluator
                .EvaluateSlowRulesAsync(Arg.Any<UTorrentItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<UTorrentItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            result.ShouldRemove.ShouldBeFalse();
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
                .GetTorrentAsync(hash)
                .Returns(torrentItem);

            _fixture.ClientWrapper
                .GetTorrentPropertiesAsync(hash)
                .Returns(torrentProperties);

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, new[] { hash });

            result.Found.ShouldBeTrue();
            result.ShouldRemove.ShouldBeFalse();
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
                .GetTorrentAsync(hash)
                .Returns(torrentItem);

            _fixture.ClientWrapper
                .GetTorrentPropertiesAsync(hash)
                .Returns(torrentProperties);

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, new[] { category });

            result.Found.ShouldBeTrue();
            result.ShouldRemove.ShouldBeFalse();
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
                .GetTorrentAsync(hash)
                .Returns(torrentItem);

            _fixture.ClientWrapper
                .GetTorrentPropertiesAsync(hash)
                .Returns(torrentProperties);

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, new[] { trackerDomain });

            result.Found.ShouldBeTrue();
            result.ShouldRemove.ShouldBeFalse();
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
                .GetTorrentAsync(hash)
                .Returns(torrentItem);

            _fixture.ClientWrapper
                .GetTorrentPropertiesAsync(hash)
                .Returns(torrentProperties);

            _fixture.ClientWrapper
                .GetTorrentFilesAsync(hash)
                .Throws(new InvalidOperationException("Failed to get files"));

            _fixture.RuleEvaluator
                .EvaluateSlowRulesAsync(Arg.Any<UTorrentItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<UTorrentItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            result.Found.ShouldBeTrue();
            result.ShouldRemove.ShouldBeFalse();
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
                .GetTorrentAsync(hash)
                .Returns(torrentItem);

            _fixture.ClientWrapper
                .GetTorrentPropertiesAsync(hash)
                .Returns(torrentProperties);

            _fixture.ClientWrapper
                .GetTorrentFilesAsync(hash)
                .Returns(new List<UTorrentFile>
                {
                    new UTorrentFile { Name = "file1.mkv", Priority = 1, Index = 0, Size = 1000, Downloaded = 500 }
                });

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<UTorrentItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            result.ShouldRemove.ShouldBeFalse();
            await _fixture.RuleEvaluator.DidNotReceive().EvaluateSlowRulesAsync(Arg.Any<UTorrentItemWrapper>());
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
                .GetTorrentAsync(hash)
                .Returns(torrentItem);

            _fixture.ClientWrapper
                .GetTorrentPropertiesAsync(hash)
                .Returns(torrentProperties);

            _fixture.ClientWrapper
                .GetTorrentFilesAsync(hash)
                .Returns(new List<UTorrentFile>
                {
                    new UTorrentFile { Name = "file1.mkv", Priority = 1, Index = 0, Size = 1000, Downloaded = 500 }
                });

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<UTorrentItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            result.ShouldRemove.ShouldBeFalse();
            await _fixture.RuleEvaluator.DidNotReceive().EvaluateSlowRulesAsync(Arg.Any<UTorrentItemWrapper>());
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
                .GetTorrentAsync(hash)
                .Returns(torrentItem);

            _fixture.ClientWrapper
                .GetTorrentPropertiesAsync(hash)
                .Returns(torrentProperties);

            _fixture.ClientWrapper
                .GetTorrentFilesAsync(hash)
                .Returns(new List<UTorrentFile>
                {
                    new UTorrentFile { Name = "file1.mkv", Priority = 1, Index = 0, Size = 1000, Downloaded = 500 }
                });

            _fixture.RuleEvaluator
                .EvaluateSlowRulesAsync(Arg.Any<UTorrentItemWrapper>())
                .Returns((true, DeleteReason.SlowSpeed, true, false));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            result.ShouldRemove.ShouldBeTrue();
            result.DeleteReason.ShouldBe(DeleteReason.SlowSpeed);
            result.DeleteFromClient.ShouldBeTrue();
            result.ChangeCategory.ShouldBeFalse();
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
                .GetTorrentAsync(hash)
                .Returns(torrentItem);

            _fixture.ClientWrapper
                .GetTorrentPropertiesAsync(hash)
                .Returns(torrentProperties);

            _fixture.ClientWrapper
                .GetTorrentFilesAsync(hash)
                .Returns(new List<UTorrentFile>
                {
                    new UTorrentFile { Name = "file1.mkv", Priority = 1, Index = 0, Size = 1000, Downloaded = 500 }
                });

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<UTorrentItemWrapper>())
                .Returns((true, DeleteReason.Stalled, true, false));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            result.ShouldRemove.ShouldBeTrue();
            result.DeleteReason.ShouldBe(DeleteReason.Stalled);
            result.DeleteFromClient.ShouldBeTrue();
            result.ChangeCategory.ShouldBeFalse();
        }

        [Fact]
        public async Task SlowDownload_RuleWithChangeCategory_PropagatesChangeCategoryFlag()
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
                .GetTorrentAsync(hash)
                .Returns(torrentItem);

            _fixture.ClientWrapper
                .GetTorrentPropertiesAsync(hash)
                .Returns(torrentProperties);

            _fixture.ClientWrapper
                .GetTorrentFilesAsync(hash)
                .Returns(new List<UTorrentFile>
                {
                    new UTorrentFile { Name = "file1.mkv", Priority = 1, Index = 0, Size = 1000, Downloaded = 500 }
                });

            _fixture.RuleEvaluator
                .EvaluateSlowRulesAsync(Arg.Any<UTorrentItemWrapper>())
                .Returns((true, DeleteReason.SlowSpeed, false, true));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            result.ShouldRemove.ShouldBeTrue();
            result.DeleteReason.ShouldBe(DeleteReason.SlowSpeed);
            result.DeleteFromClient.ShouldBeFalse();
            result.ChangeCategory.ShouldBeTrue();
        }
    }
}
