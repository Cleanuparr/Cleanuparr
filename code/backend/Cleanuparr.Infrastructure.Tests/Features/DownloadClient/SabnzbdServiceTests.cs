using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Cleanuparr.Domain.Entities.Sabnzbd;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.DownloadClient.Sabnzbd;
using Cleanuparr.Persistence.Models.Configuration.MalwareBlocker;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient;

public class SabnzbdServiceTests : IClassFixture<SabnzbdServiceFixture>
{
    private readonly SabnzbdServiceFixture _fixture;

    public SabnzbdServiceTests(SabnzbdServiceFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetMocks();
    }

    public class ShouldRemoveFromArrQueueAsync_Scenarios : SabnzbdServiceTests
    {
        public ShouldRemoveFromArrQueueAsync_Scenarios(SabnzbdServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task NotFoundInQueueOrHistory_ReturnsEmptyResult()
        {
            const string nzoId = "SABnzbd_nzo_missing";
            var sut = _fixture.CreateSut();

            _fixture.ClientWrapper.GetQueueAsync(nzoId).Returns((SabnzbdQueueData?)null);
            _fixture.ClientWrapper.GetHistoryAsync(nzoId).Returns((SabnzbdHistoryData?)null);

            var result = await sut.ShouldRemoveFromArrQueueAsync(nzoId, Array.Empty<string>());

            result.Found.ShouldBeFalse();
            result.ShouldRemove.ShouldBeFalse();
            result.DeleteReason.ShouldBe(DeleteReason.None);
        }

        [Fact]
        public async Task FoundInHistoryAsFailed_RemovesWithStalledReason_DeletesFromClient()
        {
            const string nzoId = "SABnzbd_nzo_failed";
            var sut = _fixture.CreateSut();

            _fixture.ClientWrapper.GetQueueAsync(nzoId).Returns((SabnzbdQueueData?)null);
            _fixture.ClientWrapper.GetHistoryAsync(nzoId).Returns(new SabnzbdHistoryData
            {
                Slots =
                [
                    new SabnzbdHistorySlot { NzoId = nzoId, Name = "Broken.Release", Status = "Failed", FailMessage = "not enough articles" }
                ]
            });

            var result = await sut.ShouldRemoveFromArrQueueAsync(nzoId, Array.Empty<string>());

            result.Found.ShouldBeTrue();
            result.IsPrivate.ShouldBeFalse();
            result.ShouldRemove.ShouldBeTrue();
            result.DeleteReason.ShouldBe(DeleteReason.Stalled);
            result.DeleteFromClient.ShouldBeTrue();
        }

        [Fact]
        public async Task CompletedInHistory_DoesNotRemove()
        {
            const string nzoId = "SABnzbd_nzo_completed";
            var sut = _fixture.CreateSut();

            _fixture.ClientWrapper.GetQueueAsync(nzoId).Returns((SabnzbdQueueData?)null);
            _fixture.ClientWrapper.GetHistoryAsync(nzoId).Returns(new SabnzbdHistoryData
            {
                Slots = [new SabnzbdHistorySlot { NzoId = nzoId, Name = "Good.Release", Status = "Completed", Storage = "/downloads/Good.Release" }]
            });

            var result = await sut.ShouldRemoveFromArrQueueAsync(nzoId, Array.Empty<string>());

            result.Found.ShouldBeTrue();
            result.ShouldRemove.ShouldBeFalse();
        }

        [Fact]
        public async Task IndividuallyPaused_NeverStrikes()
        {
            // A job paused on its own (not via a global pause) is still a deliberate action, not a stall.
            const string nzoId = "SABnzbd_nzo_paused";
            var sut = _fixture.CreateSut();

            _fixture.ClientWrapper.GetQueueAsync(nzoId).Returns(new SabnzbdQueueData
            {
                KbPerSec = 0,
                Slots = [new SabnzbdQueueSlot { NzoId = nzoId, Filename = "Paused.Release", Status = "Paused", Mb = 100, MbLeft = 50 }]
            });

            var result = await sut.ShouldRemoveFromArrQueueAsync(nzoId, Array.Empty<string>());

            result.Found.ShouldBeTrue();
            result.ShouldRemove.ShouldBeFalse();

            await _fixture.RuleEvaluator.DidNotReceive().EvaluateStallRulesAsync(Arg.Any<SabnzbdItemWrapper>());
            await _fixture.ClientWrapper.DidNotReceive().GetHistoryAsync(Arg.Any<string>());
        }

        [Fact]
        public async Task QueueGloballyPaused_ItemWithPartialProgress_NeverStrikes()
        {
            // A global pause leaves the slot's own status unchanged (still "Queued"), but a deliberate pause must never strike.
            const string nzoId = "SABnzbd_nzo_globally_paused_active";
            var sut = _fixture.CreateSut();

            _fixture.ClientWrapper.GetQueueAsync(nzoId).Returns(new SabnzbdQueueData
            {
                Paused = true,
                Slots = [new SabnzbdQueueSlot { NzoId = nzoId, Filename = "Paused.Release", Status = "Queued", Mb = 1715.94, MbLeft = 287.42 }]
            });

            var result = await sut.ShouldRemoveFromArrQueueAsync(nzoId, Array.Empty<string>());

            result.ShouldRemove.ShouldBeFalse();

            await _fixture.RuleEvaluator.DidNotReceive().EvaluateStallRulesAsync(Arg.Any<SabnzbdItemWrapper>());
        }

        [Fact]
        public async Task StatusDownloadingWithZeroThroughput_DoesNotRemove()
        {
            // SABnzbd fails a job that stops progressing on its own; a queue item is never struck directly.
            const string nzoId = "SABnzbd_nzo_zero_throughput";
            var sut = _fixture.CreateSut();

            _fixture.ClientWrapper.GetQueueAsync(nzoId).Returns(new SabnzbdQueueData
            {
                KbPerSec = 0,
                Slots = [new SabnzbdQueueSlot { NzoId = nzoId, Filename = "Stuck.Release", Status = "Downloading", Mb = 1000, MbLeft = 400 }]
            });

            var result = await sut.ShouldRemoveFromArrQueueAsync(nzoId, Array.Empty<string>());

            result.ShouldRemove.ShouldBeFalse();

            await _fixture.RuleEvaluator.DidNotReceive().EvaluateStallRulesAsync(Arg.Any<SabnzbdItemWrapper>());
        }

        [Fact]
        public async Task QueuedDownloadingWithSpeed_NeverEvaluatesSlowRules()
        {
            const string nzoId = "SABnzbd_nzo_downloading";
            var sut = _fixture.CreateSut();

            _fixture.ClientWrapper.GetQueueAsync(nzoId).Returns(new SabnzbdQueueData
            {
                KbPerSec = 500,
                Slots = [new SabnzbdQueueSlot { NzoId = nzoId, Filename = "Downloading.Release", Status = "Downloading", Mb = 1000, MbLeft = 400 }]
            });

            var result = await sut.ShouldRemoveFromArrQueueAsync(nzoId, Array.Empty<string>());

            result.ShouldRemove.ShouldBeFalse();

            await _fixture.RuleEvaluator.DidNotReceive().EvaluateSlowRulesAsync(Arg.Any<SabnzbdItemWrapper>());
        }

        [Fact]
        public async Task IgnoredByHash_ReturnsFoundButNotRemoved()
        {
            const string nzoId = "SABnzbd_nzo_ignored";
            var sut = _fixture.CreateSut();

            _fixture.ClientWrapper.GetQueueAsync(nzoId).Returns(new SabnzbdQueueData
            {
                Slots = [new SabnzbdQueueSlot { NzoId = nzoId, Filename = "Ignored.Release", Status = "Downloading", Mb = 100, MbLeft = 50 }]
            });

            var result = await sut.ShouldRemoveFromArrQueueAsync(nzoId, [nzoId]);

            result.Found.ShouldBeTrue();
            result.ShouldRemove.ShouldBeFalse();

            await _fixture.RuleEvaluator.DidNotReceive().EvaluateSlowRulesAsync(Arg.Any<SabnzbdItemWrapper>());
        }
    }

    public class GetSeedingDownloads_Scenarios : SabnzbdServiceTests
    {
        public GetSeedingDownloads_Scenarios(SabnzbdServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task AlwaysReturnsEmptyList()
        {
            var sut = _fixture.CreateSut();

            var result = await sut.GetSeedingDownloads();

            result.ShouldBeEmpty();
        }
    }

    public class DeleteDownload_Scenarios : SabnzbdServiceTests
    {
        public DeleteDownload_Scenarios(SabnzbdServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task HistoryItem_CallsDeleteFromHistoryAsync()
        {
            var sut = _fixture.CreateSut();
            var torrent = new SabnzbdItemWrapper(new SabnzbdHistorySlot { NzoId = "nzo1", Name = "Test", Status = "Completed" });

            await sut.DeleteDownload(torrent, true);

            await _fixture.ClientWrapper.Received(1).DeleteFromHistoryAsync("nzo1", true);
            await _fixture.ClientWrapper.DidNotReceive().DeleteFromQueueAsync(Arg.Any<string>(), Arg.Any<bool>());
        }

        [Fact]
        public async Task QueueItem_CallsDeleteFromQueueAsync()
        {
            var sut = _fixture.CreateSut();
            var torrent = new SabnzbdItemWrapper(new SabnzbdQueueSlot { NzoId = "nzo2", Filename = "Test", Status = "Downloading" }, 0);

            await sut.DeleteDownload(torrent, false);

            await _fixture.ClientWrapper.Received(1).DeleteFromQueueAsync("nzo2", false);
            await _fixture.ClientWrapper.DidNotReceive().DeleteFromHistoryAsync(Arg.Any<string>(), Arg.Any<bool>());
        }
    }

    public class BlockUnwantedFilesAsync_Scenarios : SabnzbdServiceTests
    {
        public BlockUnwantedFilesAsync_Scenarios(SabnzbdServiceFixture fixture) : base(fixture)
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

        [Fact]
        public async Task NotInQueueOrHistory_ReturnsNotFound()
        {
            const string nzoId = "SABnzbd_nzo_missing";
            var sut = _fixture.CreateSut();

            _fixture.ClientWrapper.GetQueueAsync(nzoId).Returns((SabnzbdQueueData?)null);
            _fixture.ClientWrapper.GetHistoryAsync(nzoId).Returns((SabnzbdHistoryData?)null);

            var result = await sut.BlockUnwantedFilesAsync(nzoId, Array.Empty<string>());

            result.Found.ShouldBeFalse();
        }

        [Fact]
        public async Task StillInQueue_ReturnsFoundWithoutFileScan()
        {
            const string nzoId = "SABnzbd_nzo_inprogress";
            var sut = _fixture.CreateSut();

            _fixture.ClientWrapper.GetQueueAsync(nzoId).Returns(new SabnzbdQueueData
            {
                Slots = [new SabnzbdQueueSlot { NzoId = nzoId, Filename = "Downloading.Release", Status = "Downloading", Mb = 1000, MbLeft = 400 }]
            });

            var result = await sut.BlockUnwantedFilesAsync(nzoId, Array.Empty<string>());

            result.Found.ShouldBeTrue();
            result.ShouldRemove.ShouldBeFalse();

            await _fixture.ClientWrapper.DidNotReceive().GetHistoryAsync(Arg.Any<string>());
        }

        [Fact]
        public async Task CompletedWithMissingStorageDirectory_ReturnsFoundButNoFileScan()
        {
            const string nzoId = "SABnzbd_nzo_nodir";
            var sut = _fixture.CreateSut();
            SetMalwareBlockerContext();

            _fixture.ClientWrapper.GetHistoryAsync(nzoId).Returns(new SabnzbdHistoryData
            {
                Slots = [new SabnzbdHistorySlot { NzoId = nzoId, Name = "Test", Status = "Completed", Storage = "/does/not/exist/on/disk" }]
            });

            var result = await sut.BlockUnwantedFilesAsync(nzoId, Array.Empty<string>());

            result.Found.ShouldBeTrue();
            result.ShouldRemove.ShouldBeFalse();
        }

        [Fact]
        public async Task CompletedWithMalwareFile_DeletesFileAndMarksAllFilesBlocked()
        {
            const string nzoId = "SABnzbd_nzo_malware";
            var sut = _fixture.CreateSut();
            SetMalwareBlockerContext();

            string tempDir = Directory.CreateTempSubdirectory("sabnzbd-cb-test-").FullName;
            try
            {
                string malwarePath = Path.Combine(tempDir, "malware.exe");
                await File.WriteAllTextAsync(malwarePath, "dummy");

                _fixture.ClientWrapper.GetHistoryAsync(nzoId).Returns(new SabnzbdHistoryData
                {
                    Slots = [new SabnzbdHistorySlot { NzoId = nzoId, Name = "Test", Status = "Completed", Storage = tempDir }]
                });

                _fixture.FilenameEvaluator
                    .IsValid(Arg.Any<string>(), Arg.Any<BlocklistType>(), Arg.Any<ConcurrentBag<string>>(), Arg.Any<ConcurrentBag<Regex>>())
                    .Returns(false);

                var result = await sut.BlockUnwantedFilesAsync(nzoId, Array.Empty<string>());

                result.Found.ShouldBeTrue();
                result.ShouldRemove.ShouldBeTrue();
                result.DeleteReason.ShouldBe(DeleteReason.AllFilesBlocked);
                File.Exists(malwarePath).ShouldBeFalse();
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }
    }
}
