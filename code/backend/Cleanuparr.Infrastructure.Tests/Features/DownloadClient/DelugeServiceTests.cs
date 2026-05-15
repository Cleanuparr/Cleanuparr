using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Cleanuparr.Domain.Entities.Deluge.Response;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.DownloadClient.Deluge;
using Cleanuparr.Persistence.Models.Configuration.MalwareBlocker;
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

    public class BlockUnwantedFilesAsyncScenarios : DelugeServiceTests
    {
        public BlockUnwantedFilesAsyncScenarios(DelugeServiceFixture fixture) : base(fixture)
        {
        }

        private void SetMalwareBlockerContext()
        {
            ContextProvider.Set(new ContentBlockerConfig());
            ContextProvider.Set(nameof(InstanceType), (object)InstanceType.Sonarr);

            _fixture.BlocklistProvider
                .GetBlocklistType(Arg.Any<InstanceType>())
                .Returns(BlocklistType.Blacklist);
            _fixture.BlocklistProvider
                .GetPatterns(Arg.Any<InstanceType>())
                .Returns(new ConcurrentBag<string>());
            _fixture.BlocklistProvider
                .GetRegexes(Arg.Any<InstanceType>())
                .Returns(new ConcurrentBag<Regex>());
        }

        private static DownloadStatus MakeDownloadStatus(string hash) => new()
        {
            Hash = hash,
            Name = "Malware Torrent",
            State = DelugeState.Downloading,
            Private = false,
            DownloadSpeed = 1000,
            Trackers = new List<Tracker>(),
            DownloadLocation = "/downloads",
        };

        [Fact]
        public async Task AllFilesAreMalware_DoesNotCallChangeFilesPriority_AndMarksForRemoval()
        {
            const string hash = "all-malware-hash";
            var sut = _fixture.CreateSut();
            SetMalwareBlockerContext();

            _fixture.ClientWrapper
                .GetTorrentStatus(hash)
                .Returns(MakeDownloadStatus(hash));

            _fixture.ClientWrapper
                .GetTorrentFiles(hash)
                .Returns(new DelugeContents
                {
                    Contents = new Dictionary<string, DelugeFileOrDirectory>
                    {
                        { "malware.exe", new DelugeFileOrDirectory { Type = "file", Priority = 1, Index = 0, Path = "malware.exe" } },
                    },
                });

            _fixture.FilenameEvaluator
                .IsValid(Arg.Any<string>(), Arg.Any<BlocklistType>(), Arg.Any<ConcurrentBag<string>>(), Arg.Any<ConcurrentBag<Regex>>())
                .Returns(false);

            var result = await sut.BlockUnwantedFilesAsync(hash, Array.Empty<string>());

            result.Found.ShouldBeTrue();
            result.ShouldRemove.ShouldBeTrue();
            result.DeleteReason.ShouldBe(DeleteReason.AllFilesBlocked);

            await _fixture.ClientWrapper
                .DidNotReceive()
                .ChangeFilesPriority(Arg.Any<string>(), Arg.Any<List<int>>());
        }

        [Fact]
        public async Task PartialMalware_CallsChangeFilesPriority_AndDoesNotMarkForRemoval()
        {
            const string hash = "partial-malware-hash";
            var sut = _fixture.CreateSut();
            SetMalwareBlockerContext();

            _fixture.ClientWrapper
                .GetTorrentStatus(hash)
                .Returns(MakeDownloadStatus(hash));

            _fixture.ClientWrapper
                .GetTorrentFiles(hash)
                .Returns(new DelugeContents
                {
                    Contents = new Dictionary<string, DelugeFileOrDirectory>
                    {
                        { "movie.mkv", new DelugeFileOrDirectory { Type = "file", Priority = 1, Index = 0, Path = "movie.mkv" } },
                        { "malware.exe", new DelugeFileOrDirectory { Type = "file", Priority = 1, Index = 1, Path = "malware.exe" } },
                    },
                });

            _fixture.FilenameEvaluator
                .IsValid(Arg.Is<string>(name => name.EndsWith("malware.exe")), Arg.Any<BlocklistType>(), Arg.Any<ConcurrentBag<string>>(), Arg.Any<ConcurrentBag<Regex>>())
                .Returns(false);
            _fixture.FilenameEvaluator
                .IsValid(Arg.Is<string>(name => name.EndsWith("movie.mkv")), Arg.Any<BlocklistType>(), Arg.Any<ConcurrentBag<string>>(), Arg.Any<ConcurrentBag<Regex>>())
                .Returns(true);

            var result = await sut.BlockUnwantedFilesAsync(hash, Array.Empty<string>());

            result.Found.ShouldBeTrue();
            result.ShouldRemove.ShouldBeFalse();
            result.DeleteReason.ShouldBe(DeleteReason.None);

            await _fixture.ClientWrapper
                .Received(1)
                .ChangeFilesPriority(hash, Arg.Is<List<int>>(p => p.Count == 2 && p[0] == 1 && p[1] == 0));
        }
    }
}
