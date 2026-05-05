using Cleanuparr.Domain.Entities.Deluge.Response;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.DownloadClient.Deluge;
using NSubstitute;
using Shouldly;
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
                .GetTorrentStatus(hash)
                .Returns((DownloadStatus?)null);

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

            var downloadStatus = new DownloadStatus
            {
                Hash = hash,
                Name = "Test Torrent",
                State = DelugeState.Downloading,
                Private = true,
                DownloadSpeed = 1000,
                Trackers = new List<Tracker>(),
                DownloadLocation = "/downloads"
            };

            _fixture.ClientWrapper
                .GetTorrentStatus(hash)
                .Returns(downloadStatus);

            _fixture.ClientWrapper
                .GetTorrentFiles(hash)
                .Returns(new DelugeContents
                {
                    Contents = new Dictionary<string, DelugeFileOrDirectory>
                    {
                        { "file1.mkv", new DelugeFileOrDirectory { Type = "file", Priority = 1, Index = 0 } }
                    }
                });

            _fixture.RuleEvaluator
                .EvaluateSlowRulesAsync(Arg.Any<DelugeItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<DelugeItemWrapper>())
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

            var downloadStatus = new DownloadStatus
            {
                Hash = hash,
                Name = "Test Torrent",
                State = DelugeState.Downloading,
                Private = false,
                DownloadSpeed = 1000,
                Trackers = new List<Tracker>(),
                DownloadLocation = "/downloads"
            };

            _fixture.ClientWrapper
                .GetTorrentStatus(hash)
                .Returns(downloadStatus);

            _fixture.ClientWrapper
                .GetTorrentFiles(hash)
                .Returns(new DelugeContents
                {
                    Contents = new Dictionary<string, DelugeFileOrDirectory>
                    {
                        { "file1.mkv", new DelugeFileOrDirectory { Type = "file", Priority = 1, Index = 0 } }
                    }
                });

            _fixture.RuleEvaluator
                .EvaluateSlowRulesAsync(Arg.Any<DelugeItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<DelugeItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            result.Found.ShouldBeTrue();
            result.IsPrivate.ShouldBeFalse();
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
                State = DelugeState.Downloading,
                Private = false,
                DownloadSpeed = 1000,
                Trackers = new List<Tracker>(),
                DownloadLocation = "/downloads"
            };

            _fixture.ClientWrapper
                .GetTorrentStatus(hash)
                .Returns(downloadStatus);

            _fixture.ClientWrapper
                .GetTorrentFiles(hash)
                .Returns(new DelugeContents
                {
                    Contents = new Dictionary<string, DelugeFileOrDirectory>
                    {
                        { "file1.mkv", new DelugeFileOrDirectory { Type = "file", Priority = 0, Index = 0 } },
                        { "file2.mkv", new DelugeFileOrDirectory { Type = "file", Priority = 0, Index = 1 } }
                    }
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

            var downloadStatus = new DownloadStatus
            {
                Hash = hash,
                Name = "Test Torrent",
                State = DelugeState.Downloading,
                Private = false,
                DownloadSpeed = 1000,
                Trackers = new List<Tracker>(),
                DownloadLocation = "/downloads"
            };

            _fixture.ClientWrapper
                .GetTorrentStatus(hash)
                .Returns(downloadStatus);

            _fixture.ClientWrapper
                .GetTorrentFiles(hash)
                .Returns(new DelugeContents
                {
                    Contents = new Dictionary<string, DelugeFileOrDirectory>
                    {
                        { "file1.mkv", new DelugeFileOrDirectory { Type = "file", Priority = 0, Index = 0 } },
                        { "file2.mkv", new DelugeFileOrDirectory { Type = "file", Priority = 1, Index = 1 } }
                    }
                });

            _fixture.RuleEvaluator
                .EvaluateSlowRulesAsync(Arg.Any<DelugeItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<DelugeItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            result.ShouldRemove.ShouldBeFalse();
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
                State = DelugeState.Downloading,
                Private = false,
                DownloadSpeed = 1000,
                Trackers = new List<Tracker>(),
                DownloadLocation = "/downloads"
            };

            _fixture.ClientWrapper
                .GetTorrentStatus(hash)
                .Returns(downloadStatus);

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

            var downloadStatus = new DownloadStatus
            {
                Hash = hash,
                Name = "Test Torrent",
                State = DelugeState.Downloading,
                Private = false,
                DownloadSpeed = 1000,
                Label = category,
                Trackers = new List<Tracker>(),
                DownloadLocation = "/downloads"
            };

            _fixture.ClientWrapper
                .GetTorrentStatus(hash)
                .Returns(downloadStatus);

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

            var downloadStatus = new DownloadStatus
            {
                Hash = hash,
                Name = "Test Torrent",
                State = DelugeState.Downloading,
                Private = false,
                DownloadSpeed = 1000,
                Trackers = new List<Tracker>
                {
                    new Tracker { Url = $"https://{trackerDomain}/announce" }
                },
                DownloadLocation = "/downloads"
            };

            _fixture.ClientWrapper
                .GetTorrentStatus(hash)
                .Returns(downloadStatus);

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, new[] { trackerDomain });

            result.Found.ShouldBeTrue();
            result.ShouldRemove.ShouldBeFalse();
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
                State = DelugeState.Seeding,
                Private = false,
                DownloadSpeed = 0,
                Trackers = new List<Tracker>(),
                DownloadLocation = "/downloads"
            };

            _fixture.ClientWrapper
                .GetTorrentStatus(hash)
                .Returns(downloadStatus);

            _fixture.ClientWrapper
                .GetTorrentFiles(hash)
                .Returns(new DelugeContents
                {
                    Contents = new Dictionary<string, DelugeFileOrDirectory>
                    {
                        { "file1.mkv", new DelugeFileOrDirectory { Type = "file", Priority = 1, Index = 0 } }
                    }
                });

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<DelugeItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            result.ShouldRemove.ShouldBeFalse();
            await _fixture.RuleEvaluator.DidNotReceive().EvaluateSlowRulesAsync(Arg.Any<DelugeItemWrapper>());
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
                State = DelugeState.Downloading,
                Private = false,
                DownloadSpeed = 0,
                Trackers = new List<Tracker>(),
                DownloadLocation = "/downloads"
            };

            _fixture.ClientWrapper
                .GetTorrentStatus(hash)
                .Returns(downloadStatus);

            _fixture.ClientWrapper
                .GetTorrentFiles(hash)
                .Returns(new DelugeContents
                {
                    Contents = new Dictionary<string, DelugeFileOrDirectory>
                    {
                        { "file1.mkv", new DelugeFileOrDirectory { Type = "file", Priority = 1, Index = 0 } }
                    }
                });

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<DelugeItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            result.ShouldRemove.ShouldBeFalse();
            await _fixture.RuleEvaluator.DidNotReceive().EvaluateSlowRulesAsync(Arg.Any<DelugeItemWrapper>());
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
                State = DelugeState.Downloading,
                Private = false,
                DownloadSpeed = 1000,
                Trackers = new List<Tracker>(),
                DownloadLocation = "/downloads"
            };

            _fixture.ClientWrapper
                .GetTorrentStatus(hash)
                .Returns(downloadStatus);

            _fixture.ClientWrapper
                .GetTorrentFiles(hash)
                .Returns(new DelugeContents
                {
                    Contents = new Dictionary<string, DelugeFileOrDirectory>
                    {
                        { "file1.mkv", new DelugeFileOrDirectory { Type = "file", Priority = 1, Index = 0 } }
                    }
                });

            _fixture.RuleEvaluator
                .EvaluateSlowRulesAsync(Arg.Any<DelugeItemWrapper>())
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

            var downloadStatus = new DownloadStatus
            {
                Hash = hash,
                Name = "Test Torrent",
                State = DelugeState.Downloading,
                DownloadSpeed = 0,
                Eta = 0,
                Private = false,
                Trackers = new List<Tracker>(),
                DownloadLocation = "/downloads"
            };

            _fixture.ClientWrapper
                .GetTorrentStatus(hash)
                .Returns(downloadStatus);

            _fixture.ClientWrapper
                .GetTorrentFiles(hash)
                .Returns(new DelugeContents
                {
                    Contents = new Dictionary<string, DelugeFileOrDirectory>
                    {
                        { "file1.mkv", new DelugeFileOrDirectory { Type = "file", Priority = 1, Index = 0 } }
                    }
                });

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<DelugeItemWrapper>())
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

            var downloadStatus = new DownloadStatus
            {
                Hash = hash,
                Name = "Test Torrent",
                State = DelugeState.Downloading,
                Private = false,
                DownloadSpeed = 1000,
                Trackers = new List<Tracker>(),
                DownloadLocation = "/downloads"
            };

            _fixture.ClientWrapper
                .GetTorrentStatus(hash)
                .Returns(downloadStatus);

            _fixture.ClientWrapper
                .GetTorrentFiles(hash)
                .Returns(new DelugeContents
                {
                    Contents = new Dictionary<string, DelugeFileOrDirectory>
                    {
                        { "file1.mkv", new DelugeFileOrDirectory { Type = "file", Priority = 1, Index = 0 } }
                    }
                });

            _fixture.RuleEvaluator
                .EvaluateSlowRulesAsync(Arg.Any<DelugeItemWrapper>())
                .Returns((true, DeleteReason.SlowSpeed, false, true));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            result.ShouldRemove.ShouldBeTrue();
            result.DeleteReason.ShouldBe(DeleteReason.SlowSpeed);
            result.DeleteFromClient.ShouldBeFalse();
            result.ChangeCategory.ShouldBeTrue();
        }
    }
}
