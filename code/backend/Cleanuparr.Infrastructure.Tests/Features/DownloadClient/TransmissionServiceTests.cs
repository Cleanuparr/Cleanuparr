using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.DownloadClient.Transmission;
using Moq;
using Transmission.API.RPC.Entity;
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
                TorrentFields.TOTAL_SIZE
            };

            _fixture.ClientWrapper
                .Setup(x => x.TorrentGetAsync(fields, hash))
                .ReturnsAsync((TransmissionTorrents?)null);

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
                TorrentFields.TOTAL_SIZE
            };

            _fixture.ClientWrapper
                .Setup(x => x.TorrentGetAsync(fields, hash))
                .ReturnsAsync(torrents);

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateSlowRulesAsync(It.IsAny<TransmissionItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateStallRulesAsync(It.IsAny<TransmissionItemWrapper>()))
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
                TorrentFields.TOTAL_SIZE
            };

            _fixture.ClientWrapper
                .Setup(x => x.TorrentGetAsync(fields, hash))
                .ReturnsAsync(torrents);

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateSlowRulesAsync(It.IsAny<TransmissionItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateStallRulesAsync(It.IsAny<TransmissionItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            Assert.True(result.Found);
            Assert.False(result.IsPrivate);
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
                TorrentFields.TOTAL_SIZE
            };

            _fixture.ClientWrapper
                .Setup(x => x.TorrentGetAsync(fields, hash))
                .ReturnsAsync(torrents);

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
                TorrentFields.TOTAL_SIZE
            };

            _fixture.ClientWrapper
                .Setup(x => x.TorrentGetAsync(fields, hash))
                .ReturnsAsync(torrents);

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateSlowRulesAsync(It.IsAny<TransmissionItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateStallRulesAsync(It.IsAny<TransmissionItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            Assert.False(result.ShouldRemove);
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
                TorrentFields.TOTAL_SIZE
            };

            _fixture.ClientWrapper
                .Setup(x => x.TorrentGetAsync(fields, hash))
                .ReturnsAsync(torrents);

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
                TorrentFields.TOTAL_SIZE
            };

            _fixture.ClientWrapper
                .Setup(x => x.TorrentGetAsync(fields, hash))
                .ReturnsAsync(torrents);

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, new[] { category });

            Assert.True(result.Found);
            Assert.False(result.ShouldRemove);
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
                TorrentFields.TOTAL_SIZE
            };

            _fixture.ClientWrapper
                .Setup(x => x.TorrentGetAsync(fields, hash))
                .ReturnsAsync(torrents);

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateSlowRulesAsync(It.IsAny<TransmissionItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateStallRulesAsync(It.IsAny<TransmissionItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            Assert.False(result.ShouldRemove);
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
                TorrentFields.TOTAL_SIZE
            };

            _fixture.ClientWrapper
                .Setup(x => x.TorrentGetAsync(fields, hash))
                .ReturnsAsync(torrents);

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateStallRulesAsync(It.IsAny<TransmissionItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            Assert.False(result.ShouldRemove);
            _fixture.RuleEvaluator.Verify(x => x.EvaluateSlowRulesAsync(It.IsAny<TransmissionItemWrapper>()), Times.Never);
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
                TorrentFields.TOTAL_SIZE
            };

            _fixture.ClientWrapper
                .Setup(x => x.TorrentGetAsync(fields, hash))
                .ReturnsAsync(torrents);

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateStallRulesAsync(It.IsAny<TransmissionItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            Assert.False(result.ShouldRemove);
            _fixture.RuleEvaluator.Verify(x => x.EvaluateSlowRulesAsync(It.IsAny<TransmissionItemWrapper>()), Times.Never);
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
                TorrentFields.TOTAL_SIZE
            };

            _fixture.ClientWrapper
                .Setup(x => x.TorrentGetAsync(fields, hash))
                .ReturnsAsync(torrents);

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateSlowRulesAsync(It.IsAny<TransmissionItemWrapper>()))
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
                TorrentFields.TOTAL_SIZE
            };

            _fixture.ClientWrapper
                .Setup(x => x.TorrentGetAsync(fields, hash))
                .ReturnsAsync(torrents);

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateStallRulesAsync(It.IsAny<TransmissionItemWrapper>()))
                .ReturnsAsync((true, DeleteReason.Stalled, true));

            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            Assert.True(result.ShouldRemove);
            Assert.Equal(DeleteReason.Stalled, result.DeleteReason);
            Assert.True(result.DeleteFromClient);
        }
    }
}
