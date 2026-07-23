using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.DownloadClient.Transmission;
using Cleanuparr.Persistence.Models.Configuration.MalwareBlocker;
using NSubstitute;
using Transmission.API.RPC.Arguments;
using Transmission.API.RPC.Entity;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient;

public class TransmissionServiceTests : IClassFixture<TransmissionServiceFixture>
{
    private readonly TransmissionServiceFixture _fixture;

    public TransmissionServiceTests(TransmissionServiceFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetMocks();
    }

    public class ShouldRemoveFromArrQueueAsync_BasicScenarios : TransmissionServiceTests
    {
        public ShouldRemoveFromArrQueueAsync_BasicScenarios(TransmissionServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task TorrentNotFound_ReturnsEmptyResult()
        {
            const string hash = "nonexistent";
            var sut = _fixture.CreateSut();

            _fixture.ClientWrapper
                .TorrentGetAsync(Arg.Any<string[]>(), hash)
                .Returns((TransmissionTorrents?)null);

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

            var torrentInfo = new TorrentInfo
            {
                Id = 1,
                HashString = hash,
                Name = "Test Torrent",
                Status = 4,
                IsPrivate = true,
                FileStats = new[] { new TransmissionTorrentFileStats { Wanted = true } }
            };

            var torrents = new TransmissionTorrents
            {
                Torrents = new[] { torrentInfo }
            };

            _fixture.ClientWrapper
                .TorrentGetAsync(Arg.Any<string[]>(), hash)
                .Returns(torrents);

            _fixture.RuleEvaluator
                .EvaluateSlowRulesAsync(Arg.Any<TransmissionItemWrapper>(), Arg.Any<Func<Task<bool>>?>())
                .Returns((false, DeleteReason.None, false, false));

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<TransmissionItemWrapper>())
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

            var torrentInfo = new TorrentInfo
            {
                Id = 1,
                HashString = hash,
                Name = "Test Torrent",
                Status = 4,
                IsPrivate = false,
                FileStats = new[] { new TransmissionTorrentFileStats { Wanted = true } }
            };

            var torrents = new TransmissionTorrents
            {
                Torrents = new[] { torrentInfo }
            };

            _fixture.ClientWrapper
                .TorrentGetAsync(Arg.Any<string[]>(), hash)
                .Returns(torrents);

            _fixture.RuleEvaluator
                .EvaluateSlowRulesAsync(Arg.Any<TransmissionItemWrapper>(), Arg.Any<Func<Task<bool>>?>())
                .Returns((false, DeleteReason.None, false, false));

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<TransmissionItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            result.Found.ShouldBeTrue();
            result.IsPrivate.ShouldBeFalse();
        }
    }

    public class ShouldRemoveFromArrQueueAsync_AllFilesSkippedScenarios : TransmissionServiceTests
    {
        public ShouldRemoveFromArrQueueAsync_AllFilesSkippedScenarios(TransmissionServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task AllFilesUnwanted_DeletesFromClient()
        {
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            var torrentInfo = new TorrentInfo
            {
                Id = 1,
                HashString = hash,
                Name = "Test Torrent",
                Status = 4,
                IsPrivate = false,
                FileStats = new[]
                {
                    new TransmissionTorrentFileStats { Wanted = false },
                    new TransmissionTorrentFileStats { Wanted = false }
                }
            };

            var torrents = new TransmissionTorrents
            {
                Torrents = new[] { torrentInfo }
            };

            _fixture.ClientWrapper
                .TorrentGetAsync(Arg.Any<string[]>(), hash)
                .Returns(torrents);

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

            var torrentInfo = new TorrentInfo
            {
                Id = 1,
                HashString = hash,
                Name = "Test Torrent",
                Status = 4,
                IsPrivate = false,
                RateDownload = 1000,
                FileStats = new[]
                {
                    new TransmissionTorrentFileStats { Wanted = false },
                    new TransmissionTorrentFileStats { Wanted = true }
                }
            };

            var torrents = new TransmissionTorrents
            {
                Torrents = new[] { torrentInfo }
            };

            _fixture.ClientWrapper
                .TorrentGetAsync(Arg.Any<string[]>(), hash)
                .Returns(torrents);

            _fixture.RuleEvaluator
                .EvaluateSlowRulesAsync(Arg.Any<TransmissionItemWrapper>(), Arg.Any<Func<Task<bool>>?>())
                .Returns((false, DeleteReason.None, false, false));

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<TransmissionItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            result.ShouldRemove.ShouldBeFalse();
        }
    }

    public class ShouldRemoveFromArrQueueAsync_IgnoredDownloadScenarios : TransmissionServiceTests
    {
        public ShouldRemoveFromArrQueueAsync_IgnoredDownloadScenarios(TransmissionServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task TorrentIgnoredByHash_ReturnsEmptyResult()
        {
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            var torrentInfo = new TorrentInfo
            {
                Id = 1,
                HashString = hash,
                Name = "Test Torrent",
                Status = 4,
                IsPrivate = false,
                FileStats = new[] { new TransmissionTorrentFileStats { Wanted = true } }
            };

            var torrents = new TransmissionTorrents
            {
                Torrents = new[] { torrentInfo }
            };

            _fixture.ClientWrapper
                .TorrentGetAsync(Arg.Any<string[]>(), hash)
                .Returns(torrents);

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

            var torrentInfo = new TorrentInfo
            {
                Id = 1,
                HashString = hash,
                Name = "Test Torrent",
                Status = 4,
                IsPrivate = false,
                Labels = new[] { category },
                FileStats = new[] { new TransmissionTorrentFileStats { Wanted = true } }
            };

            var torrents = new TransmissionTorrents
            {
                Torrents = new[] { torrentInfo }
            };

            _fixture.ClientWrapper
                .TorrentGetAsync(Arg.Any<string[]>(), hash)
                .Returns(torrents);

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, new[] { category });

            result.Found.ShouldBeTrue();
            result.ShouldRemove.ShouldBeFalse();
        }

    }

    public class ShouldRemoveFromArrQueueAsync_MissingFileStatsScenarios : TransmissionServiceTests
    {
        public ShouldRemoveFromArrQueueAsync_MissingFileStatsScenarios(TransmissionServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task FilesWithMissingWantedStatus_DoesNotRemove()
        {
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            var torrentInfo = new TorrentInfo
            {
                Id = 1,
                HashString = hash,
                Name = "Test Torrent",
                Status = 4,
                IsPrivate = false,
                RateDownload = 1000,
                FileStats = new[]
                {
                    new TransmissionTorrentFileStats { Wanted = null },
                    new TransmissionTorrentFileStats { Wanted = false }
                }
            };

            var torrents = new TransmissionTorrents
            {
                Torrents = new[] { torrentInfo }
            };

            _fixture.ClientWrapper
                .TorrentGetAsync(Arg.Any<string[]>(), hash)
                .Returns(torrents);

            _fixture.RuleEvaluator
                .EvaluateSlowRulesAsync(Arg.Any<TransmissionItemWrapper>(), Arg.Any<Func<Task<bool>>?>())
                .Returns((false, DeleteReason.None, false, false));

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<TransmissionItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            result.ShouldRemove.ShouldBeFalse();
        }
    }

    public class ShouldRemoveFromArrQueueAsync_StateCheckScenarios : TransmissionServiceTests
    {
        public ShouldRemoveFromArrQueueAsync_StateCheckScenarios(TransmissionServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task NotDownloadingState_SkipsSlowCheck()
        {
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            var torrentInfo = new TorrentInfo
            {
                Id = 1,
                HashString = hash,
                Name = "Test Torrent",
                Status = 6,
                IsPrivate = false,
                RateDownload = 0,
                FileStats = new[] { new TransmissionTorrentFileStats { Wanted = true } }
            };

            var torrents = new TransmissionTorrents
            {
                Torrents = new[] { torrentInfo }
            };

            _fixture.ClientWrapper
                .TorrentGetAsync(Arg.Any<string[]>(), hash)
                .Returns(torrents);

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<TransmissionItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            result.ShouldRemove.ShouldBeFalse();
            await _fixture.RuleEvaluator.DidNotReceive().EvaluateSlowRulesAsync(Arg.Any<TransmissionItemWrapper>(), Arg.Any<Func<Task<bool>>?>());
        }

        [Fact]
        public async Task ZeroDownloadSpeed_SkipsSlowCheck()
        {
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            var torrentInfo = new TorrentInfo
            {
                Id = 1,
                HashString = hash,
                Name = "Test Torrent",
                Status = 4,
                IsPrivate = false,
                RateDownload = 0,
                FileStats = new[] { new TransmissionTorrentFileStats { Wanted = true } }
            };

            var torrents = new TransmissionTorrents
            {
                Torrents = new[] { torrentInfo }
            };

            _fixture.ClientWrapper
                .TorrentGetAsync(Arg.Any<string[]>(), hash)
                .Returns(torrents);

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<TransmissionItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            result.ShouldRemove.ShouldBeFalse();
            await _fixture.RuleEvaluator.DidNotReceive().EvaluateSlowRulesAsync(Arg.Any<TransmissionItemWrapper>(), Arg.Any<Func<Task<bool>>?>());
        }
    }

    public class ShouldRemoveFromArrQueueAsync_SlowAndStalledScenarios : TransmissionServiceTests
    {
        public ShouldRemoveFromArrQueueAsync_SlowAndStalledScenarios(TransmissionServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task SlowDownload_MatchesRule_RemovesFromQueue()
        {
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            var torrentInfo = new TorrentInfo
            {
                Id = 1,
                HashString = hash,
                Name = "Test Torrent",
                Status = 4,
                IsPrivate = false,
                RateDownload = 1000,
                FileStats = new[] { new TransmissionTorrentFileStats { Wanted = true } }
            };

            var torrents = new TransmissionTorrents
            {
                Torrents = new[] { torrentInfo }
            };

            _fixture.ClientWrapper
                .TorrentGetAsync(Arg.Any<string[]>(), hash)
                .Returns(torrents);

            _fixture.RuleEvaluator
                .EvaluateSlowRulesAsync(Arg.Any<TransmissionItemWrapper>(), Arg.Any<Func<Task<bool>>?>())
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

            var torrentInfo = new TorrentInfo
            {
                Id = 1,
                HashString = hash,
                Name = "Test Torrent",
                Status = 4,
                RateDownload = 0,
                Eta = 0,
                IsPrivate = false,
                FileStats = new[] { new TransmissionTorrentFileStats { Wanted = true } }
            };

            var torrents = new TransmissionTorrents
            {
                Torrents = new[] { torrentInfo }
            };

            _fixture.ClientWrapper
                .TorrentGetAsync(Arg.Any<string[]>(), hash)
                .Returns(torrents);

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<TransmissionItemWrapper>())
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

            var torrentInfo = new TorrentInfo
            {
                Id = 1,
                HashString = hash,
                Name = "Test Torrent",
                Status = 4,
                IsPrivate = false,
                RateDownload = 1000,
                FileStats = new[] { new TransmissionTorrentFileStats { Wanted = true } }
            };

            var torrents = new TransmissionTorrents
            {
                Torrents = new[] { torrentInfo }
            };

            _fixture.ClientWrapper
                .TorrentGetAsync(Arg.Any<string[]>(), hash)
                .Returns(torrents);

            _fixture.RuleEvaluator
                .EvaluateSlowRulesAsync(Arg.Any<TransmissionItemWrapper>(), Arg.Any<Func<Task<bool>>?>())
                .Returns((true, DeleteReason.SlowSpeed, false, true));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            result.ShouldRemove.ShouldBeTrue();
            result.DeleteReason.ShouldBe(DeleteReason.SlowSpeed);
            result.DeleteFromClient.ShouldBeFalse();
            result.ChangeCategory.ShouldBeTrue();
        }
    }

    public class BlockUnwantedFilesAsyncScenarios : TransmissionServiceTests
    {
        public BlockUnwantedFilesAsyncScenarios(TransmissionServiceFixture fixture) : base(fixture)
        {
        }

        private void SetMalwareBlockerContext(ContentBlockerConfig? config = null)
        {
            ContextProvider.Set(config ?? new ContentBlockerConfig());
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

        private void StubClient(string hash, (string Name, bool Wanted)[] files, bool isPrivate = false)
        {
            TorrentInfo torrentInfo = new()
            {
                Id = 42,
                HashString = hash,
                Name = "Malware Torrent",
                Status = 4,
                IsPrivate = isPrivate,
                Files = files.Select(f => new TransmissionTorrentFiles { Name = f.Name }).ToArray(),
                FileStats = files.Select(f => new TransmissionTorrentFileStats { Wanted = f.Wanted }).ToArray(),
            };

            _fixture.ClientWrapper
                .TorrentGetAsync(Arg.Any<string[]>(), hash)
                .Returns(new TransmissionTorrents { Torrents = new[] { torrentInfo } });
        }

        [Fact]
        public async Task AllFilesAreMalware_MarksForRemoval_WithAllFilesBlockedReason()
        {
            const string hash = "all-malware-hash";
            TransmissionService sut = _fixture.CreateSut();
            SetMalwareBlockerContext();

            StubClient(hash, [("malware.exe", true)]);

            _fixture.FilenameEvaluator
                .IsValid(Arg.Any<string>(), Arg.Any<BlocklistType>(), Arg.Any<ConcurrentBag<string>>(), Arg.Any<ConcurrentBag<Regex>>())
                .Returns(false);

            BlockFilesResult result = await sut.BlockUnwantedFilesAsync(hash, Array.Empty<string>());

            result.Found.ShouldBeTrue();
            result.ShouldRemove.ShouldBeTrue();
            result.DeleteReason.ShouldBe(DeleteReason.AllFilesBlocked);
        }

        [Fact]
        public async Task PartialMalware_CallsTorrentSet_AndDoesNotMarkForRemoval()
        {
            const string hash = "partial-malware-hash";
            TransmissionService sut = _fixture.CreateSut();
            SetMalwareBlockerContext();

            StubClient(hash, [("movie.mkv", true), ("installer.exe", true)]);

            _fixture.FilenameEvaluator
                .IsValid(Arg.Is<string>(name => name.EndsWith("installer.exe")), Arg.Any<BlocklistType>(), Arg.Any<ConcurrentBag<string>>(), Arg.Any<ConcurrentBag<Regex>>())
                .Returns(false);
            _fixture.FilenameEvaluator
                .IsValid(Arg.Is<string>(name => name.EndsWith("movie.mkv")), Arg.Any<BlocklistType>(), Arg.Any<ConcurrentBag<string>>(), Arg.Any<ConcurrentBag<Regex>>())
                .Returns(true);

            BlockFilesResult result = await sut.BlockUnwantedFilesAsync(hash, Array.Empty<string>());

            result.Found.ShouldBeTrue();
            result.ShouldRemove.ShouldBeFalse();
            result.DeleteReason.ShouldBe(DeleteReason.None);

            await _fixture.ClientWrapper
                .Received(1)
                .TorrentSetAsync(Arg.Any<TorrentSettings>());
        }

        [Fact]
        public async Task PartialMalware_WithDeleteIfAnyFileBlocked_MarksForRemoval_AndSkipsTorrentSet()
        {
            const string hash = "partial-malware-any-hash";
            TransmissionService sut = _fixture.CreateSut();
            SetMalwareBlockerContext(new ContentBlockerConfig { DeleteIfAnyFileBlocked = true });

            StubClient(hash, [("movie.mkv", true), ("installer.exe", true)]);

            _fixture.FilenameEvaluator
                .IsValid(Arg.Is<string>(name => name.EndsWith("installer.exe")), Arg.Any<BlocklistType>(), Arg.Any<ConcurrentBag<string>>(), Arg.Any<ConcurrentBag<Regex>>())
                .Returns(false);
            _fixture.FilenameEvaluator
                .IsValid(Arg.Is<string>(name => name.EndsWith("movie.mkv")), Arg.Any<BlocklistType>(), Arg.Any<ConcurrentBag<string>>(), Arg.Any<ConcurrentBag<Regex>>())
                .Returns(true);

            BlockFilesResult result = await sut.BlockUnwantedFilesAsync(hash, Array.Empty<string>());

            result.Found.ShouldBeTrue();
            result.ShouldRemove.ShouldBeTrue();
            result.DeleteReason.ShouldBe(DeleteReason.AtLeastOneFileBlocked);

            await _fixture.ClientWrapper
                .DidNotReceive()
                .TorrentSetAsync(Arg.Any<TorrentSettings>());
        }

        [Fact]
        public async Task NoUnwantedFiles_DoesNotMarkForRemoval()
        {
            const string hash = "clean-hash";
            TransmissionService sut = _fixture.CreateSut();
            SetMalwareBlockerContext();

            StubClient(hash, [("movie.mkv", true)]);

            _fixture.FilenameEvaluator
                .IsValid(Arg.Any<string>(), Arg.Any<BlocklistType>(), Arg.Any<ConcurrentBag<string>>(), Arg.Any<ConcurrentBag<Regex>>())
                .Returns(true);

            BlockFilesResult result = await sut.BlockUnwantedFilesAsync(hash, Array.Empty<string>());

            result.Found.ShouldBeTrue();
            result.ShouldRemove.ShouldBeFalse();
            result.DeleteReason.ShouldBe(DeleteReason.None);

            await _fixture.ClientWrapper
                .DidNotReceive()
                .TorrentSetAsync(Arg.Any<TorrentSettings>());
        }

        [Fact]
        public async Task AlreadyUnwantedFile_DoesNotTriggerEarlyReturn_WhenDeleteIfAnyFileBlocked()
        {
            const string hash = "already-skipped-hash";
            TransmissionService sut = _fixture.CreateSut();
            SetMalwareBlockerContext(new ContentBlockerConfig { DeleteIfAnyFileBlocked = true });

            StubClient(hash, [("movie.mkv", true), ("installer.exe", false)]);

            _fixture.FilenameEvaluator
                .IsValid(Arg.Is<string>(name => name.EndsWith("movie.mkv")), Arg.Any<BlocklistType>(), Arg.Any<ConcurrentBag<string>>(), Arg.Any<ConcurrentBag<Regex>>())
                .Returns(true);

            BlockFilesResult result = await sut.BlockUnwantedFilesAsync(hash, Array.Empty<string>());

            result.Found.ShouldBeTrue();
            result.ShouldRemove.ShouldBeFalse();
            result.DeleteReason.ShouldBe(DeleteReason.None);
        }
    }
}
