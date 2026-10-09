using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.Sabnzbd;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.DownloadClient.Sabnzbd;
using Cleanuparr.Persistence.Models.Configuration;
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
        public async Task FoundInHistoryAsFailed_RemovesWithDownloadFailedReason_DeletesFromClient()
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
            result.DeleteReason.ShouldBe(DeleteReason.DownloadFailed);
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
        }

        [Fact]
        public async Task QueuedDownloadingWithSpeed_DoesNotRemove()
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
        }
    }

    public class GetClaimedPathsAsync_Scenarios : SabnzbdServiceTests
    {
        public GetClaimedPathsAsync_Scenarios(SabnzbdServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task HistoryItem_ClaimsStoragePath()
        {
            var sut = _fixture.CreateSut();
            var item = new SabnzbdItemWrapper(new SabnzbdHistorySlot { NzoId = "nzo1", Name = "Test", Status = "Completed", Storage = "/downloads/complete/Test" });

            IReadOnlyList<string> claimed = await sut.GetClaimedPathsAsync([item]);

            claimed.ShouldContain("/downloads/complete/Test");
        }

        [Fact]
        public async Task HistoryItem_RemapsStoragePath()
        {
            string targetRoot = Directory.CreateTempSubdirectory("sabnzbd-claim-test-").FullName;

            try
            {
                DownloadClientConfig config = new()
                {
                    Id = Guid.NewGuid(),
                    Name = "Test Client",
                    TypeName = Domain.Enums.DownloadClientTypeName.Sabnzbd,
                    Type = Domain.Enums.DownloadClientType.Usenet,
                    Enabled = true,
                    Host = new Uri("http://localhost:8080"),
                    ApiKey = "test-api-key",
                    UrlBase = "",
                    DownloadDirectorySource = "/downloads",
                    DownloadDirectoryTarget = targetRoot,
                };
                var sut = _fixture.CreateSut(config);
                var item = new SabnzbdItemWrapper(new SabnzbdHistorySlot { NzoId = "nzo1", Name = "Test", Status = "Completed", Storage = "/downloads/complete/Test" });

                IReadOnlyList<string> claimed = await sut.GetClaimedPathsAsync([item]);

                claimed.ShouldContain(Path.Combine(targetRoot, "complete", "Test"));
            }
            finally
            {
                Directory.Delete(targetRoot, true);
            }
        }

        [Fact]
        public async Task BusyQueue_ClaimsDownloadDirEntriesAndCompleteDir()
        {
            // SAB can rename a folder on a clash, so a busy queue claims every entry, not just name lookalikes.
            string downloadDir = Directory.CreateTempSubdirectory("sabnzbd-incomplete-").FullName;
            string completeDir = Directory.CreateTempSubdirectory("sabnzbd-complete-").FullName;

            try
            {
                string jobFolder = Directory.CreateDirectory(Path.Combine(downloadDir, "Downloading.Release")).FullName;
                string renamedFolder = Directory.CreateDirectory(Path.Combine(downloadDir, "Downloading.Release.1")).FullName;

                var sut = _fixture.CreateSut();
                _fixture.ClientWrapper.GetDownloadDirAsync().Returns(downloadDir);
                _fixture.ClientWrapper.GetCompleteDirAsync().Returns(completeDir);
                var item = new SabnzbdItemWrapper(new SabnzbdQueueSlot { NzoId = "nzo2", Filename = "Downloading.Release", Status = "Downloading", Mb = 100, MbLeft = 50 });

                IReadOnlyList<string> claimed = await sut.GetClaimedPathsAsync([item]);

                claimed.ShouldContain(downloadDir);
                claimed.ShouldContain(jobFolder);
                claimed.ShouldContain(renamedFolder);
                claimed.ShouldContain(completeDir);
            }
            finally
            {
                Directory.Delete(downloadDir, true);
                Directory.Delete(completeDir, true);
            }
        }

        [Fact]
        public async Task EmptyQueue_ClaimsNothingFromDownloadDir()
        {
            // Nothing is downloading, so download_dir leftovers count as orphans, not claims.
            string downloadDir = Directory.CreateTempSubdirectory("sabnzbd-incomplete-").FullName;

            try
            {
                Directory.CreateDirectory(Path.Combine(downloadDir, "Leftover.Release"));

                var sut = _fixture.CreateSut();
                _fixture.ClientWrapper.GetDownloadDirAsync().Returns(downloadDir);
                var item = new SabnzbdItemWrapper(new SabnzbdHistorySlot { NzoId = "nzo1", Name = "Test", Status = "Completed", Storage = "/downloads/complete/Test" });

                IReadOnlyList<string> claimed = await sut.GetClaimedPathsAsync([item]);

                claimed.ShouldNotContain(Path.Combine(downloadDir, "Leftover.Release"));
            }
            finally
            {
                Directory.Delete(downloadDir, true);
            }
        }

        [Fact]
        public async Task HistoryOnlyPostProcessing_ClaimsEveryEntryInDownloadDir()
        {
            // SAB moves a job to history before post-processing finishes, so an empty queue with a busy history item must still claim.
            string downloadDir = Directory.CreateTempSubdirectory("sabnzbd-incomplete-").FullName;

            try
            {
                string jobFolder = Directory.CreateDirectory(Path.Combine(downloadDir, "Extracting.Release")).FullName;

                var sut = _fixture.CreateSut();
                _fixture.ClientWrapper.GetDownloadDirAsync().Returns(downloadDir);
                var item = new SabnzbdItemWrapper(new SabnzbdHistorySlot { NzoId = "nzo1", Name = "Extracting.Release", Status = "Extracting" });

                IReadOnlyList<string> claimed = await sut.GetClaimedPathsAsync([item]);

                claimed.ShouldContain(jobFolder);
            }
            finally
            {
                Directory.Delete(downloadDir, true);
            }
        }

        [Fact]
        public async Task HistoryOnlyCompleted_ClaimsNothingFromDownloadDir()
        {
            string downloadDir = Directory.CreateTempSubdirectory("sabnzbd-incomplete-").FullName;

            try
            {
                Directory.CreateDirectory(Path.Combine(downloadDir, "Leftover.Release"));

                var sut = _fixture.CreateSut();
                _fixture.ClientWrapper.GetDownloadDirAsync().Returns(downloadDir);
                var item = new SabnzbdItemWrapper(new SabnzbdHistorySlot { NzoId = "nzo1", Name = "Test", Status = "Completed", Storage = "/downloads/complete/Test" });

                IReadOnlyList<string> claimed = await sut.GetClaimedPathsAsync([item]);

                claimed.ShouldNotContain(Path.Combine(downloadDir, "Leftover.Release"));
            }
            finally
            {
                Directory.Delete(downloadDir, true);
            }
        }

        [Fact]
        public async Task BusyQueue_DownloadDirMissingOnDisk_Throws()
        {
            var sut = _fixture.CreateSut();
            _fixture.ClientWrapper.GetDownloadDirAsync().Returns("/does/not/exist/on/disk");
            var item = new SabnzbdItemWrapper(new SabnzbdQueueSlot { NzoId = "nzo2", Filename = "Downloading.Release", Status = "Downloading", Mb = 100, MbLeft = 50 });

            await Should.ThrowAsync<InvalidOperationException>(() => sut.GetClaimedPathsAsync([item]));
        }

        [Fact]
        public async Task BusyQueue_EmptyDownloadDir_Throws()
        {
            var sut = _fixture.CreateSut();
            _fixture.ClientWrapper.GetDownloadDirAsync().Returns((string?)null);
            var item = new SabnzbdItemWrapper(new SabnzbdQueueSlot { NzoId = "nzo2", Filename = "Downloading.Release", Status = "Downloading", Mb = 100, MbLeft = 50 });

            await Should.ThrowAsync<InvalidOperationException>(() => sut.GetClaimedPathsAsync([item]));
        }

        [Fact]
        public async Task BusyQueue_RelativeDownloadDir_Throws()
        {
            var sut = _fixture.CreateSut();
            _fixture.ClientWrapper.GetDownloadDirAsync().Returns("incomplete");
            var item = new SabnzbdItemWrapper(new SabnzbdQueueSlot { NzoId = "nzo2", Filename = "Downloading.Release", Status = "Downloading", Mb = 100, MbLeft = 50 });

            await Should.ThrowAsync<InvalidOperationException>(() => sut.GetClaimedPathsAsync([item]));
        }

        [Fact]
        public async Task IdleClient_UnresolvableDownloadDirAndCompleteDir_SkipsBothWithoutThrowing()
        {
            // An idle client never needs BuildIncompleteClaimsAsync's throwing resolve, so a missing dir is skipped.
            var sut = _fixture.CreateSut();
            _fixture.ClientWrapper.GetDownloadDirAsync().Returns((string?)null);
            _fixture.ClientWrapper.GetCompleteDirAsync().Returns("/does/not/exist/on/disk");
            var item = new SabnzbdItemWrapper(new SabnzbdHistorySlot { NzoId = "nzo1", Name = "Test", Status = "Completed", Storage = "/downloads/complete/Test" });

            IReadOnlyList<string> claimed = await sut.GetClaimedPathsAsync([item]);

            claimed.ShouldContain("/downloads/complete/Test");
            claimed.ShouldNotContain("/does/not/exist/on/disk");
        }

        [Fact]
        public async Task IdleClient_EmptyHistory_ClaimsDownloadDirAndCompleteDirOnly()
        {
            string downloadDir = Directory.CreateTempSubdirectory("sabnzbd-incomplete-").FullName;
            string completeDir = Directory.CreateTempSubdirectory("sabnzbd-complete-").FullName;

            try
            {
                string leftover = Directory.CreateDirectory(Path.Combine(downloadDir, "Leftover.Release")).FullName;

                var sut = _fixture.CreateSut();
                _fixture.ClientWrapper.GetDownloadDirAsync().Returns(downloadDir);
                _fixture.ClientWrapper.GetCompleteDirAsync().Returns(completeDir);

                IReadOnlyList<string> claimed = await sut.GetClaimedPathsAsync([]);

                claimed.ShouldContain(downloadDir);
                claimed.ShouldContain(completeDir);
                claimed.ShouldNotContain(leftover);
            }
            finally
            {
                Directory.Delete(downloadDir, true);
                Directory.Delete(completeDir, true);
            }
        }

        [Fact]
        public async Task BusyQueue_WindowsDownloadDirRemappedToExistingLinuxDir_ClaimsItsEntries()
        {
            // Remap happens before the rooted check, so a Windows SAB dir resolves through a configured remap.
            string targetRoot = Directory.CreateTempSubdirectory("sabnzbd-incomplete-remap-").FullName;

            try
            {
                string jobFolder = Directory.CreateDirectory(Path.Combine(targetRoot, "Downloading.Release")).FullName;

                DownloadClientConfig config = new()
                {
                    Id = Guid.NewGuid(),
                    Name = "Test Client",
                    TypeName = Domain.Enums.DownloadClientTypeName.Sabnzbd,
                    Type = Domain.Enums.DownloadClientType.Usenet,
                    Enabled = true,
                    Host = new Uri("http://localhost:8080"),
                    ApiKey = "test-api-key",
                    UrlBase = "",
                    DownloadDirectorySource = "D:\\incomplete",
                    DownloadDirectoryTarget = targetRoot,
                };
                var sut = _fixture.CreateSut(config);
                _fixture.ClientWrapper.GetDownloadDirAsync().Returns("D:\\incomplete");
                var item = new SabnzbdItemWrapper(new SabnzbdQueueSlot { NzoId = "nzo2", Filename = "Downloading.Release", Status = "Downloading", Mb = 100, MbLeft = 50 });

                IReadOnlyList<string> claimed = await sut.GetClaimedPathsAsync([item]);

                claimed.ShouldContain(jobFolder);
            }
            finally
            {
                Directory.Delete(targetRoot, true);
            }
        }

        [Fact]
        public async Task CategorySubfolderHistoryItem_ClaimsOnlyTheJobFolder()
        {
            // The ancestors above the job folder (complete/tv, complete) are no longer claimed directly here;
            // the orphan scanner's ancestor-of-a-claim rule keeps them instead.
            string targetRoot = Directory.CreateTempSubdirectory("sabnzbd-claim-test-").FullName;

            try
            {
                DownloadClientConfig config = new()
                {
                    Id = Guid.NewGuid(),
                    Name = "Test Client",
                    TypeName = Domain.Enums.DownloadClientTypeName.Sabnzbd,
                    Type = Domain.Enums.DownloadClientType.Usenet,
                    Enabled = true,
                    Host = new Uri("http://localhost:8080"),
                    ApiKey = "test-api-key",
                    UrlBase = "",
                    DownloadDirectorySource = "/downloads",
                    DownloadDirectoryTarget = targetRoot,
                };
                var sut = _fixture.CreateSut(config);
                var item = new SabnzbdItemWrapper(new SabnzbdHistorySlot
                {
                    NzoId = "nzo1", Name = "Job", Status = "Completed", Storage = "/downloads/complete/tv/Job"
                });

                IReadOnlyList<string> claimed = await sut.GetClaimedPathsAsync([item]);

                claimed.ShouldContain(Path.Combine(targetRoot, "complete", "tv", "Job"));
                claimed.ShouldNotContain(Path.Combine(targetRoot, "complete", "tv"));
                claimed.ShouldNotContain(Path.Combine(targetRoot, "complete"));
            }
            finally
            {
                Directory.Delete(targetRoot, true);
            }
        }

        [Fact]
        public async Task SingleFileHistoryItem_ClaimsOnlyTheStorageFile()
        {
            // A single-file job reports `storage` as the file itself; its job folder is left to the scanner's
            // ancestor-of-a-claim rule rather than claimed here.
            string targetRoot = Directory.CreateTempSubdirectory("sabnzbd-claim-test-").FullName;

            try
            {
                DownloadClientConfig config = new()
                {
                    Id = Guid.NewGuid(),
                    Name = "Test Client",
                    TypeName = Domain.Enums.DownloadClientTypeName.Sabnzbd,
                    Type = Domain.Enums.DownloadClientType.Usenet,
                    Enabled = true,
                    Host = new Uri("http://localhost:8080"),
                    ApiKey = "test-api-key",
                    UrlBase = "",
                    DownloadDirectorySource = "/downloads",
                    DownloadDirectoryTarget = targetRoot,
                };
                var sut = _fixture.CreateSut(config);
                var item = new SabnzbdItemWrapper(new SabnzbdHistorySlot
                {
                    NzoId = "nzo1", Name = "Job", Status = "Completed", Storage = "/downloads/complete/Job/file.mkv"
                });

                IReadOnlyList<string> claimed = await sut.GetClaimedPathsAsync([item]);

                claimed.ShouldContain(Path.Combine(targetRoot, "complete", "Job", "file.mkv"));
                claimed.ShouldNotContain(Path.Combine(targetRoot, "complete", "Job"));
            }
            finally
            {
                Directory.Delete(targetRoot, true);
            }
        }

        [Fact]
        public async Task AbsoluteCategoryHistoryItem_ClaimsTheJobFolderOutsideCompleteDir()
        {
            // No download_dir mapping covers this storage path, so it needs no remapping.
            var sut = _fixture.CreateSut();
            var item = new SabnzbdItemWrapper(new SabnzbdHistorySlot
            {
                NzoId = "nzo1", Name = "Job", Status = "Completed", Storage = "/data/tv/Job"
            });

            IReadOnlyList<string> claimed = await sut.GetClaimedPathsAsync([item]);

            claimed.ShouldContain("/data/tv/Job");
        }
    }

    public class GetAllDownloadsLite_Scenarios : SabnzbdServiceTests
    {
        public GetAllDownloadsLite_Scenarios(SabnzbdServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task NullQueue_Throws()
        {
            var sut = _fixture.CreateSut();
            _fixture.ClientWrapper.GetQueueAsync().Returns((SabnzbdQueueData?)null);
            _fixture.ClientWrapper.GetHistoryAsync().Returns(new SabnzbdHistoryData { Slots = [] });

            await Should.ThrowAsync<InvalidOperationException>(() => sut.GetAllDownloadsLite());
        }

        [Fact]
        public async Task NullHistory_Throws()
        {
            var sut = _fixture.CreateSut();
            _fixture.ClientWrapper.GetQueueAsync().Returns(new SabnzbdQueueData { Slots = [] });
            _fixture.ClientWrapper.GetHistoryAsync().Returns((SabnzbdHistoryData?)null);

            await Should.ThrowAsync<InvalidOperationException>(() => sut.GetAllDownloadsLite());
        }

        [Fact]
        public async Task BlankNzoId_Throws()
        {
            var sut = _fixture.CreateSut();
            _fixture.ClientWrapper.GetQueueAsync().Returns(new SabnzbdQueueData
            {
                Slots = [new SabnzbdQueueSlot { NzoId = "", Filename = "Blank.Release", Status = "Downloading" }]
            });
            _fixture.ClientWrapper.GetHistoryAsync().Returns(new SabnzbdHistoryData { Slots = [] });

            await Should.ThrowAsync<InvalidOperationException>(() => sut.GetAllDownloadsLite());
        }

        [Fact]
        public async Task HealthyResponse_ReturnsEveryRow()
        {
            var sut = _fixture.CreateSut();
            _fixture.ClientWrapper.GetQueueAsync().Returns(new SabnzbdQueueData
            {
                Slots = [new SabnzbdQueueSlot { NzoId = "nzo1", Filename = "Queued.Release", Status = "Downloading" }]
            });
            _fixture.ClientWrapper.GetHistoryAsync().Returns(new SabnzbdHistoryData
            {
                Slots = [new SabnzbdHistorySlot { NzoId = "nzo2", Name = "Done.Release", Status = "Completed" }]
            });

            List<IDownloadItem> items = await sut.GetAllDownloadsLite();

            items.Count.ShouldBe(2);
            items.Select(x => x.DownloadId).ShouldBe(["nzo1", "nzo2"], ignoreOrder: true);
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
            var torrent = new SabnzbdItemWrapper(new SabnzbdQueueSlot { NzoId = "nzo2", Filename = "Test", Status = "Downloading" });

            await sut.DeleteDownload(torrent, false);

            await _fixture.ClientWrapper.Received(1).DeleteFromQueueAsync("nzo2", false);
            await _fixture.ClientWrapper.DidNotReceive().DeleteFromHistoryAsync(Arg.Any<string>(), Arg.Any<bool>());
        }

        [Fact]
        public async Task CompletedWithDeleteFiles_DeletesStorageFolderFromDisk()
        {
            var sut = _fixture.CreateSut();
            string tempDir = Directory.CreateTempSubdirectory("sabnzbd-delete-test-").FullName;

            try
            {
                var torrent = new SabnzbdItemWrapper(new SabnzbdHistorySlot { NzoId = "nzo1", Name = "Test", Status = "Completed", Storage = tempDir });

                await sut.DeleteDownload(torrent, true);

                Directory.Exists(tempDir).ShouldBeFalse();
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public async Task CompletedSingleFileJob_LastFileInFolder_KeepsTheFolder()
        {
            // With job folders off, the file sits straight in complete_dir; emptying it must not remove complete_dir.
            var sut = _fixture.CreateSut();
            string completeDir = Directory.CreateTempSubdirectory("sabnzbd-delete-test-").FullName;
            string storagePath = Path.Combine(completeDir, "file.mkv");
            await File.WriteAllTextAsync(storagePath, "x");

            try
            {
                var torrent = new SabnzbdItemWrapper(new SabnzbdHistorySlot { NzoId = "nzo1", Name = "Test", Status = "Completed", Storage = storagePath });

                await sut.DeleteDownload(torrent, true);

                File.Exists(storagePath).ShouldBeFalse();
                Directory.Exists(completeDir).ShouldBeTrue();
            }
            finally
            {
                Directory.Delete(completeDir, true);
            }
        }

        [Fact]
        public async Task CompletedSingleFileJob_SharedParentWithSiblingFile_DeletesOnlyTheJobFile()
        {
            // SAB sorting can put another job's file in the same folder; the folder must survive.
            var sut = _fixture.CreateSut();
            string sharedParent = Directory.CreateTempSubdirectory("sabnzbd-delete-test-").FullName;
            string storagePath = Path.Combine(sharedParent, "file.mkv");
            string siblingPath = Path.Combine(sharedParent, "other-job.mkv");
            await File.WriteAllTextAsync(storagePath, "x");
            await File.WriteAllTextAsync(siblingPath, "y");

            try
            {
                var torrent = new SabnzbdItemWrapper(new SabnzbdHistorySlot { NzoId = "nzo1", Name = "Test", Status = "Completed", Storage = storagePath });

                await sut.DeleteDownload(torrent, true);

                File.Exists(storagePath).ShouldBeFalse();
                File.Exists(siblingPath).ShouldBeTrue();
                Directory.Exists(sharedParent).ShouldBeTrue();
            }
            finally
            {
                Directory.Delete(sharedParent, true);
            }
        }

        [Theory]
        [InlineData("Test")]
        [InlineData("Test.1")]
        public async Task CompletedSingleFileJob_EmptiedJobFolder_RemovesTheFolder(string jobFolderName)
        {
            SabnzbdService sut = _fixture.CreateSut();
            string completeDir = Directory.CreateTempSubdirectory("sabnzbd-delete-test-").FullName;
            string jobFolder = Path.Combine(completeDir, jobFolderName);
            string storagePath = Path.Combine(jobFolder, "file.mkv");
            Directory.CreateDirectory(jobFolder);
            await File.WriteAllTextAsync(storagePath, "x");

            try
            {
                SabnzbdItemWrapper torrent = new(new SabnzbdHistorySlot { NzoId = "nzo1", Name = "Test", Status = "Completed", Storage = storagePath });

                await sut.DeleteDownload(torrent, true);

                Directory.Exists(jobFolder).ShouldBeFalse();
                Directory.Exists(completeDir).ShouldBeTrue();
            }
            finally
            {
                Directory.Delete(completeDir, true);
            }
        }

        [Fact]
        public async Task CompletedSingleFileJob_JobFolderWithLeftoverFile_KeepsTheFolder()
        {
            SabnzbdService sut = _fixture.CreateSut();
            string completeDir = Directory.CreateTempSubdirectory("sabnzbd-delete-test-").FullName;
            string jobFolder = Path.Combine(completeDir, "Test");
            string storagePath = Path.Combine(jobFolder, "file.mkv");
            string leftoverPath = Path.Combine(jobFolder, "file.nfo");
            Directory.CreateDirectory(jobFolder);
            await File.WriteAllTextAsync(storagePath, "x");
            await File.WriteAllTextAsync(leftoverPath, "y");

            try
            {
                SabnzbdItemWrapper torrent = new(new SabnzbdHistorySlot { NzoId = "nzo1", Name = "Test", Status = "Completed", Storage = storagePath });

                await sut.DeleteDownload(torrent, true);

                File.Exists(storagePath).ShouldBeFalse();
                File.Exists(leftoverPath).ShouldBeTrue();
            }
            finally
            {
                Directory.Delete(completeDir, true);
            }
        }

        [Fact]
        public async Task CompletedSingleFileJob_NoJobName_KeepsTheFolder()
        {
            // An empty name prefixes every folder, so it must not count as a match.
            SabnzbdService sut = _fixture.CreateSut();
            string completeDir = Directory.CreateTempSubdirectory("sabnzbd-delete-test-").FullName;
            string storagePath = Path.Combine(completeDir, "file.mkv");
            await File.WriteAllTextAsync(storagePath, "x");

            try
            {
                SabnzbdItemWrapper torrent = new(new SabnzbdHistorySlot { NzoId = "nzo1", Name = "", Status = "Completed", Storage = storagePath });

                await sut.DeleteDownload(torrent, true);

                File.Exists(storagePath).ShouldBeFalse();
                Directory.Exists(completeDir).ShouldBeTrue();
            }
            finally
            {
                Directory.Delete(completeDir, true);
            }
        }

        [Fact]
        public async Task FailedStatus_DoesNotDeleteFromDisk()
        {
            var sut = _fixture.CreateSut();
            string tempDir = Directory.CreateTempSubdirectory("sabnzbd-delete-test-").FullName;

            try
            {
                var torrent = new SabnzbdItemWrapper(new SabnzbdHistorySlot { NzoId = "nzo1", Name = "Test", Status = "Failed", Storage = tempDir });

                await sut.DeleteDownload(torrent, true);

                Directory.Exists(tempDir).ShouldBeTrue();
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task DeleteFilesFalse_DoesNotDeleteFromDisk()
        {
            var sut = _fixture.CreateSut();
            string tempDir = Directory.CreateTempSubdirectory("sabnzbd-delete-test-").FullName;

            try
            {
                var torrent = new SabnzbdItemWrapper(new SabnzbdHistorySlot { NzoId = "nzo1", Name = "Test", Status = "Completed", Storage = tempDir });

                await sut.DeleteDownload(torrent, false);

                Directory.Exists(tempDir).ShouldBeTrue();
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task QueueItem_NeverDeletesFromDisk()
        {
            var sut = _fixture.CreateSut();
            var torrent = new SabnzbdItemWrapper(new SabnzbdQueueSlot { NzoId = "nzo2", Filename = "Test", Status = "Downloading" });

            await sut.DeleteDownload(torrent, true);

            await _fixture.DryRunInterceptor.DidNotReceive().InterceptAsync(Arg.Any<Func<Task>>(), Arg.Any<string?>());
        }

        [Fact]
        public async Task MissingStoragePath_DoesNotThrow()
        {
            var sut = _fixture.CreateSut();
            var torrent = new SabnzbdItemWrapper(new SabnzbdHistorySlot { NzoId = "nzo1", Name = "Test", Status = "Completed", Storage = null });

            await Should.NotThrowAsync(() => sut.DeleteDownload(torrent, true));

            await _fixture.ClientWrapper.Received(1).DeleteFromHistoryAsync("nzo1", true);
        }

        [Fact]
        public async Task DryRun_SkipsDiskDelete_ButStillDeletesFromHistory()
        {
            var sut = _fixture.CreateSut();
            string tempDir = Directory.CreateTempSubdirectory("sabnzbd-delete-test-").FullName;

            try
            {
                var torrent = new SabnzbdItemWrapper(new SabnzbdHistorySlot { NzoId = "nzo1", Name = "Test", Status = "Completed", Storage = tempDir });

                _fixture.DryRunInterceptor
                    .InterceptAsync(Arg.Any<Func<Task>>(), Arg.Any<string?>())
                    .Returns(Task.CompletedTask);

                await sut.DeleteDownload(torrent, true);

                Directory.Exists(tempDir).ShouldBeTrue();
                await _fixture.ClientWrapper.Received(1).DeleteFromHistoryAsync("nzo1", true);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
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

        [Fact]
        public async Task CompletedSingleFileJob_WithMalwareFile_DeletesFileAndMarksAllFilesBlocked()
        {
            const string nzoId = "SABnzbd_nzo_malware_singlefile";
            SabnzbdService sut = _fixture.CreateSut();
            SetMalwareBlockerContext();

            string tempDir = Directory.CreateTempSubdirectory("sabnzbd-cb-test-").FullName;
            try
            {
                string malwarePath = Path.Combine(tempDir, "malware.exe");
                await File.WriteAllTextAsync(malwarePath, "dummy");

                _fixture.ClientWrapper.GetHistoryAsync(nzoId).Returns(new SabnzbdHistoryData
                {
                    Slots = [new SabnzbdHistorySlot { NzoId = nzoId, Name = "Test", Status = "Completed", Storage = malwarePath }]
                });

                _fixture.FilenameEvaluator
                    .IsValid(Arg.Any<string>(), Arg.Any<BlocklistType>(), Arg.Any<ConcurrentBag<string>>(), Arg.Any<ConcurrentBag<Regex>>())
                    .Returns(false);

                BlockFilesResult result = await sut.BlockUnwantedFilesAsync(nzoId, Array.Empty<string>());

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

    public class LoginAndHealthCheck_Scenarios : SabnzbdServiceTests
    {
        public LoginAndHealthCheck_Scenarios(SabnzbdServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task LoginAsync_ValidatesTheApiKey()
        {
            var sut = _fixture.CreateSut();
            _fixture.ClientWrapper.ValidateApiKeyAsync().Returns(Task.CompletedTask);

            await sut.LoginAsync();

            await _fixture.ClientWrapper.Received(1).ValidateApiKeyAsync();
        }

        [Fact]
        public async Task LoginAsync_RejectedApiKey_Throws()
        {
            var sut = _fixture.CreateSut();
            _fixture.ClientWrapper.ValidateApiKeyAsync().Returns(Task.FromException(new SabnzbdClientException("SABnzbd request failed for mode 'queue': Forbidden API Key Incorrect")));

            await Should.ThrowAsync<SabnzbdClientException>(() => sut.LoginAsync());
        }

        [Fact]
        public async Task HealthCheckAsync_RejectedApiKey_ReportsUnhealthy()
        {
            var sut = _fixture.CreateSut();
            _fixture.ClientWrapper.ValidateApiKeyAsync().Returns(Task.FromException(new SabnzbdClientException("SABnzbd request failed for mode 'queue': Forbidden API Key Incorrect")));

            var result = await sut.HealthCheckAsync();

            result.IsHealthy.ShouldBeFalse();
            result.ErrorMessage.ShouldContain("API Key Incorrect");
        }
    }
}
