using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.DownloadClient.Transmission;
using NSubstitute;
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

            var fields = new[]
            {
                TorrentFields.FILES,
                TorrentFields.FILE_STATS,
                TorrentFields.HASH_STRING,
                TorrentFields.ID,
                TorrentFields.ETA,
                TorrentFields.NAME,
                TorrentFields.STATUS,
                TorrentFields.IS_PRIVATE,
                TorrentFields.DOWNLOADED_EVER,
                TorrentFields.DOWNLOAD_DIR,
                TorrentFields.SECONDS_SEEDING,
                TorrentFields.UPLOAD_RATIO,
                TorrentFields.TRACKERS,
                TorrentFields.RATE_DOWNLOAD,
                TorrentFields.TOTAL_SIZE,
                TorrentFields.LABELS
            };

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

            var fields = new[]
            {
                TorrentFields.FILES,
                TorrentFields.FILE_STATS,
                TorrentFields.HASH_STRING,
                TorrentFields.ID,
                TorrentFields.ETA,
                TorrentFields.NAME,
                TorrentFields.STATUS,
                TorrentFields.IS_PRIVATE,
                TorrentFields.DOWNLOADED_EVER,
                TorrentFields.DOWNLOAD_DIR,
                TorrentFields.SECONDS_SEEDING,
                TorrentFields.UPLOAD_RATIO,
                TorrentFields.TRACKERS,
                TorrentFields.RATE_DOWNLOAD,
                TorrentFields.TOTAL_SIZE,
                TorrentFields.LABELS
            };

            _fixture.ClientWrapper
                .TorrentGetAsync(Arg.Any<string[]>(), hash)
                .Returns(torrents);

            _fixture.RuleEvaluator
                .EvaluateSlowRulesAsync(Arg.Any<TransmissionItemWrapper>())
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

            var fields = new[]
            {
                TorrentFields.FILES,
                TorrentFields.FILE_STATS,
                TorrentFields.HASH_STRING,
                TorrentFields.ID,
                TorrentFields.ETA,
                TorrentFields.NAME,
                TorrentFields.STATUS,
                TorrentFields.IS_PRIVATE,
                TorrentFields.DOWNLOADED_EVER,
                TorrentFields.DOWNLOAD_DIR,
                TorrentFields.SECONDS_SEEDING,
                TorrentFields.UPLOAD_RATIO,
                TorrentFields.TRACKERS,
                TorrentFields.RATE_DOWNLOAD,
                TorrentFields.TOTAL_SIZE,
                TorrentFields.LABELS
            };

            _fixture.ClientWrapper
                .TorrentGetAsync(Arg.Any<string[]>(), hash)
                .Returns(torrents);

            _fixture.RuleEvaluator
                .EvaluateSlowRulesAsync(Arg.Any<TransmissionItemWrapper>())
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

            var fields = new[]
            {
                TorrentFields.FILES,
                TorrentFields.FILE_STATS,
                TorrentFields.HASH_STRING,
                TorrentFields.ID,
                TorrentFields.ETA,
                TorrentFields.NAME,
                TorrentFields.STATUS,
                TorrentFields.IS_PRIVATE,
                TorrentFields.DOWNLOADED_EVER,
                TorrentFields.DOWNLOAD_DIR,
                TorrentFields.SECONDS_SEEDING,
                TorrentFields.UPLOAD_RATIO,
                TorrentFields.TRACKERS,
                TorrentFields.RATE_DOWNLOAD,
                TorrentFields.TOTAL_SIZE,
                TorrentFields.LABELS
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

            var fields = new[]
            {
                TorrentFields.FILES,
                TorrentFields.FILE_STATS,
                TorrentFields.HASH_STRING,
                TorrentFields.ID,
                TorrentFields.ETA,
                TorrentFields.NAME,
                TorrentFields.STATUS,
                TorrentFields.IS_PRIVATE,
                TorrentFields.DOWNLOADED_EVER,
                TorrentFields.DOWNLOAD_DIR,
                TorrentFields.SECONDS_SEEDING,
                TorrentFields.UPLOAD_RATIO,
                TorrentFields.TRACKERS,
                TorrentFields.RATE_DOWNLOAD,
                TorrentFields.TOTAL_SIZE,
                TorrentFields.LABELS
            };

            _fixture.ClientWrapper
                .TorrentGetAsync(Arg.Any<string[]>(), hash)
                .Returns(torrents);

            _fixture.RuleEvaluator
                .EvaluateSlowRulesAsync(Arg.Any<TransmissionItemWrapper>())
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

            var fields = new[]
            {
                TorrentFields.FILES,
                TorrentFields.FILE_STATS,
                TorrentFields.HASH_STRING,
                TorrentFields.ID,
                TorrentFields.ETA,
                TorrentFields.NAME,
                TorrentFields.STATUS,
                TorrentFields.IS_PRIVATE,
                TorrentFields.DOWNLOADED_EVER,
                TorrentFields.DOWNLOAD_DIR,
                TorrentFields.SECONDS_SEEDING,
                TorrentFields.UPLOAD_RATIO,
                TorrentFields.TRACKERS,
                TorrentFields.RATE_DOWNLOAD,
                TorrentFields.TOTAL_SIZE,
                TorrentFields.LABELS
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

            var fields = new[]
            {
                TorrentFields.FILES,
                TorrentFields.FILE_STATS,
                TorrentFields.HASH_STRING,
                TorrentFields.ID,
                TorrentFields.ETA,
                TorrentFields.NAME,
                TorrentFields.STATUS,
                TorrentFields.IS_PRIVATE,
                TorrentFields.DOWNLOADED_EVER,
                TorrentFields.DOWNLOAD_DIR,
                TorrentFields.SECONDS_SEEDING,
                TorrentFields.UPLOAD_RATIO,
                TorrentFields.TRACKERS,
                TorrentFields.RATE_DOWNLOAD,
                TorrentFields.TOTAL_SIZE,
                TorrentFields.LABELS
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

            var fields = new[]
            {
                TorrentFields.FILES,
                TorrentFields.FILE_STATS,
                TorrentFields.HASH_STRING,
                TorrentFields.ID,
                TorrentFields.ETA,
                TorrentFields.NAME,
                TorrentFields.STATUS,
                TorrentFields.IS_PRIVATE,
                TorrentFields.DOWNLOADED_EVER,
                TorrentFields.DOWNLOAD_DIR,
                TorrentFields.SECONDS_SEEDING,
                TorrentFields.UPLOAD_RATIO,
                TorrentFields.TRACKERS,
                TorrentFields.RATE_DOWNLOAD,
                TorrentFields.TOTAL_SIZE,
                TorrentFields.LABELS
            };

            _fixture.ClientWrapper
                .TorrentGetAsync(Arg.Any<string[]>(), hash)
                .Returns(torrents);

            _fixture.RuleEvaluator
                .EvaluateSlowRulesAsync(Arg.Any<TransmissionItemWrapper>())
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

            var fields = new[]
            {
                TorrentFields.FILES,
                TorrentFields.FILE_STATS,
                TorrentFields.HASH_STRING,
                TorrentFields.ID,
                TorrentFields.ETA,
                TorrentFields.NAME,
                TorrentFields.STATUS,
                TorrentFields.IS_PRIVATE,
                TorrentFields.DOWNLOADED_EVER,
                TorrentFields.DOWNLOAD_DIR,
                TorrentFields.SECONDS_SEEDING,
                TorrentFields.UPLOAD_RATIO,
                TorrentFields.TRACKERS,
                TorrentFields.RATE_DOWNLOAD,
                TorrentFields.TOTAL_SIZE,
                TorrentFields.LABELS
            };

            _fixture.ClientWrapper
                .TorrentGetAsync(Arg.Any<string[]>(), hash)
                .Returns(torrents);

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<TransmissionItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            result.ShouldRemove.ShouldBeFalse();
            await _fixture.RuleEvaluator.DidNotReceive().EvaluateSlowRulesAsync(Arg.Any<TransmissionItemWrapper>());
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

            var fields = new[]
            {
                TorrentFields.FILES,
                TorrentFields.FILE_STATS,
                TorrentFields.HASH_STRING,
                TorrentFields.ID,
                TorrentFields.ETA,
                TorrentFields.NAME,
                TorrentFields.STATUS,
                TorrentFields.IS_PRIVATE,
                TorrentFields.DOWNLOADED_EVER,
                TorrentFields.DOWNLOAD_DIR,
                TorrentFields.SECONDS_SEEDING,
                TorrentFields.UPLOAD_RATIO,
                TorrentFields.TRACKERS,
                TorrentFields.RATE_DOWNLOAD,
                TorrentFields.TOTAL_SIZE,
                TorrentFields.LABELS
            };

            _fixture.ClientWrapper
                .TorrentGetAsync(Arg.Any<string[]>(), hash)
                .Returns(torrents);

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<TransmissionItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            result.ShouldRemove.ShouldBeFalse();
            await _fixture.RuleEvaluator.DidNotReceive().EvaluateSlowRulesAsync(Arg.Any<TransmissionItemWrapper>());
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

            var fields = new[]
            {
                TorrentFields.FILES,
                TorrentFields.FILE_STATS,
                TorrentFields.HASH_STRING,
                TorrentFields.ID,
                TorrentFields.ETA,
                TorrentFields.NAME,
                TorrentFields.STATUS,
                TorrentFields.IS_PRIVATE,
                TorrentFields.DOWNLOADED_EVER,
                TorrentFields.DOWNLOAD_DIR,
                TorrentFields.SECONDS_SEEDING,
                TorrentFields.UPLOAD_RATIO,
                TorrentFields.TRACKERS,
                TorrentFields.RATE_DOWNLOAD,
                TorrentFields.TOTAL_SIZE,
                TorrentFields.LABELS
            };

            _fixture.ClientWrapper
                .TorrentGetAsync(Arg.Any<string[]>(), hash)
                .Returns(torrents);

            _fixture.RuleEvaluator
                .EvaluateSlowRulesAsync(Arg.Any<TransmissionItemWrapper>())
                .Returns((true, DeleteReason.SlowSpeed, true, false));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            result.ShouldRemove.ShouldBeTrue();
            result.DeleteReason.ShouldBe(DeleteReason.SlowSpeed);
            result.DeleteFromClient.ShouldBeTrue();
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

            var fields = new[]
            {
                TorrentFields.FILES,
                TorrentFields.FILE_STATS,
                TorrentFields.HASH_STRING,
                TorrentFields.ID,
                TorrentFields.ETA,
                TorrentFields.NAME,
                TorrentFields.STATUS,
                TorrentFields.IS_PRIVATE,
                TorrentFields.DOWNLOADED_EVER,
                TorrentFields.DOWNLOAD_DIR,
                TorrentFields.SECONDS_SEEDING,
                TorrentFields.UPLOAD_RATIO,
                TorrentFields.TRACKERS,
                TorrentFields.RATE_DOWNLOAD,
                TorrentFields.TOTAL_SIZE,
                TorrentFields.LABELS
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
        }
    }
}
