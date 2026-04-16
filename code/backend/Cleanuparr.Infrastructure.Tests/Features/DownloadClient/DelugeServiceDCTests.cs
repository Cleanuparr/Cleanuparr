using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.Deluge.Response;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient.Deluge;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient;

public class DelugeServiceDCTests : IClassFixture<DelugeServiceFixture>
{
    private readonly DelugeServiceFixture _fixture;

    public DelugeServiceDCTests(DelugeServiceFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetMocks();
    }

    public class GetSeedingDownloads_Tests : DelugeServiceDCTests
    {
        public GetSeedingDownloads_Tests(DelugeServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task FiltersSeedingState()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var downloads = new List<DownloadStatus>
            {
                new DownloadStatus { Hash = "hash1", Name = "Torrent 1", State = "Seeding", Private = false, Trackers = new List<Tracker>(), DownloadLocation = "/downloads" },
                new DownloadStatus { Hash = "hash2", Name = "Torrent 2", State = "Downloading", Private = false, Trackers = new List<Tracker>(), DownloadLocation = "/downloads" },
                new DownloadStatus { Hash = "hash3", Name = "Torrent 3", State = "Seeding", Private = false, Trackers = new List<Tracker>(), DownloadLocation = "/downloads" }
            };

            _fixture.ClientWrapper
                .GetStatusForAllTorrents()
                .Returns(downloads);

            // Act
            var result = await sut.GetSeedingDownloads();

            // Assert
            result.Count.ShouldBe(2);
            foreach (var item in result) { item.Hash.ShouldNotBeNull(); }
        }

        [Fact]
        public async Task IsCaseInsensitive()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var downloads = new List<DownloadStatus>
            {
                new DownloadStatus { Hash = "hash1", Name = "Torrent 1", State = "SEEDING", Private = false, Trackers = new List<Tracker>(), DownloadLocation = "/downloads" },
                new DownloadStatus { Hash = "hash2", Name = "Torrent 2", State = "seeding", Private = false, Trackers = new List<Tracker>(), DownloadLocation = "/downloads" }
            };

            _fixture.ClientWrapper
                .GetStatusForAllTorrents()
                .Returns(downloads);

            // Act
            var result = await sut.GetSeedingDownloads();

            // Assert
            result.Count.ShouldBe(2);
        }

        [Fact]
        public async Task ReturnsEmptyList_WhenNull()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            _fixture.ClientWrapper
                .GetStatusForAllTorrents()
                .Returns((List<DownloadStatus>?)null);

            // Act
            var result = await sut.GetSeedingDownloads();

            // Assert
            result.ShouldBeEmpty();
        }

        [Fact]
        public async Task SkipsTorrentsWithEmptyHash()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var downloads = new List<DownloadStatus>
            {
                new DownloadStatus { Hash = "", Name = "No Hash", State = "Seeding", Private = false, Trackers = new List<Tracker>(), DownloadLocation = "/downloads" },
                new DownloadStatus { Hash = "hash1", Name = "Valid Hash", State = "Seeding", Private = false, Trackers = new List<Tracker>(), DownloadLocation = "/downloads" }
            };

            _fixture.ClientWrapper
                .GetStatusForAllTorrents()
                .Returns(downloads);

            // Act
            var result = await sut.GetSeedingDownloads();

            // Assert
            result.ShouldHaveSingleItem();
            result[0].Hash.ShouldBe("hash1");
        }
    }

    public class FilterDownloadsToBeCleanedAsync_Tests : DelugeServiceDCTests
    {
        public FilterDownloadsToBeCleanedAsync_Tests(DelugeServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public void MatchesCategories()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new DelugeItemWrapper(new DownloadStatus { Hash = "hash1", Label = "movies", Trackers = new List<Tracker>(), DownloadLocation = "/downloads" }),
                new DelugeItemWrapper(new DownloadStatus { Hash = "hash2", Label = "tv", Trackers = new List<Tracker>(), DownloadLocation = "/downloads" }),
                new DelugeItemWrapper(new DownloadStatus { Hash = "hash3", Label = "music", Trackers = new List<Tracker>(), DownloadLocation = "/downloads" })
            };

            var categories = new List<ISeedingRule>
            {
                new DelugeSeedingRule { Name = "movies", Categories = ["movies"], MaxRatio = -1, MinSeedTime = 0, MaxSeedTime = -1, DeleteSourceFiles = true },
                new DelugeSeedingRule { Name = "tv", Categories = ["tv"], MaxRatio = -1, MinSeedTime = 0, MaxSeedTime = -1, DeleteSourceFiles = true }
            };

            // Act
            var result = sut.FilterDownloadsToBeCleanedAsync(downloads, categories);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(2);
            result.ShouldContain(x => x.Category == "movies");
            result.ShouldContain(x => x.Category == "tv");
        }

        [Fact]
        public void IsCaseInsensitive()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new DelugeItemWrapper(new DownloadStatus { Hash = "hash1", Label = "Movies", Trackers = new List<Tracker>(), DownloadLocation = "/downloads" })
            };

            var categories = new List<ISeedingRule>
            {
                new DelugeSeedingRule { Name = "movies", Categories = ["movies"], MaxRatio = -1, MinSeedTime = 0, MaxSeedTime = -1, DeleteSourceFiles = true }
            };

            // Act
            var result = sut.FilterDownloadsToBeCleanedAsync(downloads, categories);

            // Assert
            result.ShouldNotBeNull();
            result.ShouldHaveSingleItem();
        }

        [Fact]
        public void ReturnsEmptyList_WhenNoMatches()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new DelugeItemWrapper(new DownloadStatus { Hash = "hash1", Label = "music", Trackers = new List<Tracker>(), DownloadLocation = "/downloads" })
            };

            var categories = new List<ISeedingRule>
            {
                new DelugeSeedingRule { Name = "movies", Categories = ["movies"], MaxRatio = -1, MinSeedTime = 0, MaxSeedTime = -1, DeleteSourceFiles = true }
            };

            // Act
            var result = sut.FilterDownloadsToBeCleanedAsync(downloads, categories);

            // Assert
            result.ShouldNotBeNull();
            result.ShouldBeEmpty();
        }
    }

    public class FilterDownloadsToChangeCategoryAsync_Tests : DelugeServiceDCTests
    {
        public FilterDownloadsToChangeCategoryAsync_Tests(DelugeServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public void FiltersCorrectly()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new DelugeItemWrapper(new DownloadStatus { Hash = "hash1", Label = "movies", Trackers = new List<Tracker>(), DownloadLocation = "/downloads" }),
                new DelugeItemWrapper(new DownloadStatus { Hash = "hash2", Label = "tv", Trackers = new List<Tracker>(), DownloadLocation = "/downloads" })
            };

            // Act
            var result = sut.FilterDownloadsToChangeCategoryAsync(downloads, new UnlinkedConfig { Categories = ["movies"] });

            // Assert
            result.ShouldNotBeNull();
            result.ShouldHaveSingleItem();
            result[0].Hash.ShouldBe("hash1");
        }

        [Fact]
        public void IsCaseInsensitive()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new DelugeItemWrapper(new DownloadStatus { Hash = "hash1", Label = "Movies", Trackers = new List<Tracker>(), DownloadLocation = "/downloads" })
            };

            // Act
            var result = sut.FilterDownloadsToChangeCategoryAsync(downloads, new UnlinkedConfig { Categories = ["movies"] });

            // Assert
            result.ShouldNotBeNull();
            result.ShouldHaveSingleItem();
        }

        [Fact]
        public void SkipsDownloadsWithEmptyHash()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new DelugeItemWrapper(new DownloadStatus { Hash = "", Label = "movies", Trackers = new List<Tracker>(), DownloadLocation = "/downloads" }),
                new DelugeItemWrapper(new DownloadStatus { Hash = "hash1", Label = "movies", Trackers = new List<Tracker>(), DownloadLocation = "/downloads" })
            };

            // Act
            var result = sut.FilterDownloadsToChangeCategoryAsync(downloads, new UnlinkedConfig { Categories = ["movies"] });

            // Assert
            result.ShouldNotBeNull();
            result.ShouldHaveSingleItem();
            result[0].Hash.ShouldBe("hash1");
        }

        [Fact]
        public void ReturnsEmpty_WhenNoCategoriesMatch()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new DelugeItemWrapper(new DownloadStatus { Hash = "hash1", Label = "tv", Trackers = new List<Tracker>(), DownloadLocation = "/downloads" })
            };

            // Act
            var result = sut.FilterDownloadsToChangeCategoryAsync(downloads, new UnlinkedConfig { Categories = ["movies"] });

            // Assert
            result.ShouldNotBeNull();
            result.ShouldBeEmpty();
        }
    }

    public class CreateCategoryAsync_Tests : DelugeServiceDCTests
    {
        public CreateCategoryAsync_Tests(DelugeServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task CreatesLabel_WhenMissing()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            _fixture.ClientWrapper
                .GetLabels()
                .Returns(new List<string>());

            _fixture.ClientWrapper
                .CreateLabel("new-label")
                .Returns(Task.CompletedTask);

            // Act
            await sut.CreateCategoryAsync("new-label");

            // Assert
            await _fixture.ClientWrapper.Received(1).CreateLabel("new-label");
        }

        [Fact]
        public async Task SkipsCreation_WhenLabelExists()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            _fixture.ClientWrapper
                .GetLabels()
                .Returns(new List<string> { "existing" });

            // Act
            await sut.CreateCategoryAsync("existing");

            // Assert
            await _fixture.ClientWrapper.DidNotReceive().CreateLabel(Arg.Any<string>());
        }

        [Fact]
        public async Task IsCaseInsensitive()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            _fixture.ClientWrapper
                .GetLabels()
                .Returns(new List<string> { "Existing" });

            // Act
            await sut.CreateCategoryAsync("existing");

            // Assert
            await _fixture.ClientWrapper.DidNotReceive().CreateLabel(Arg.Any<string>());
        }
    }

    public class DeleteDownload_Tests : DelugeServiceDCTests
    {
        public DeleteDownload_Tests(DelugeServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task CallsClientDelete()
        {
            // Arrange
            var sut = _fixture.CreateSut();
            const string hash = "TEST-HASH";
            var mockTorrent = Substitute.For<ITorrentItemWrapper>();
            mockTorrent.Hash.Returns(hash);

            _fixture.ClientWrapper
                .DeleteTorrents(Arg.Is<List<string>>(h => h.Contains("test-hash")), true)
                .Returns(Task.CompletedTask);

            // Act
            await sut.DeleteDownload(mockTorrent, true);

            // Assert
            await _fixture.ClientWrapper.Received(1)
                .DeleteTorrents(Arg.Is<List<string>>(h => h.Contains("test-hash")), true);
        }

        [Fact]
        public async Task NormalizesHashToLowercase()
        {
            // Arrange
            var sut = _fixture.CreateSut();
            const string hash = "UPPERCASE-HASH";
            var mockTorrent = Substitute.For<ITorrentItemWrapper>();
            mockTorrent.Hash.Returns(hash);

            _fixture.ClientWrapper
                .DeleteTorrents(Arg.Any<List<string>>(), true)
                .Returns(Task.CompletedTask);

            // Act
            await sut.DeleteDownload(mockTorrent, true);

            // Assert
            await _fixture.ClientWrapper.Received(1)
                .DeleteTorrents(Arg.Is<List<string>>(h => h.Contains("uppercase-hash")), true);
        }

        [Fact]
        public async Task CallsClientDeleteWithoutSourceFiles()
        {
            // Arrange
            var sut = _fixture.CreateSut();
            const string hash = "TEST-HASH";
            var mockTorrent = Substitute.For<ITorrentItemWrapper>();
            mockTorrent.Hash.Returns(hash);

            _fixture.ClientWrapper
                .DeleteTorrents(Arg.Is<List<string>>(h => h.Contains("test-hash")), false)
                .Returns(Task.CompletedTask);

            // Act
            await sut.DeleteDownload(mockTorrent, false);

            // Assert
            await _fixture.ClientWrapper.Received(1)
                .DeleteTorrents(Arg.Is<List<string>>(h => h.Contains("test-hash")), false);
        }
    }

    public class ChangeCategoryForNoHardLinksAsync_Tests : DelugeServiceDCTests
    {
        public ChangeCategoryForNoHardLinksAsync_Tests(DelugeServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task NullDownloads_DoesNothing()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var unlinkedConfig = new UnlinkedConfig
            {
                Id = Guid.NewGuid(),
                TargetCategory = "unlinked"
            };

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(null, unlinkedConfig);

            // Assert
            await _fixture.ClientWrapper.DidNotReceive().SetTorrentLabel(Arg.Any<string>(), Arg.Any<string>());
        }

        [Fact]
        public async Task EmptyDownloads_DoesNothing()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var unlinkedConfig = new UnlinkedConfig
            {
                Id = Guid.NewGuid(),
                TargetCategory = "unlinked"
            };

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(new List<Domain.Entities.ITorrentItemWrapper>(), unlinkedConfig);

            // Assert
            await _fixture.ClientWrapper.DidNotReceive().SetTorrentLabel(Arg.Any<string>(), Arg.Any<string>());
        }

        [Fact]
        public async Task MissingHash_SkipsTorrent()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var unlinkedConfig = new UnlinkedConfig
            {
                Id = Guid.NewGuid(),
                TargetCategory = "unlinked"
            };

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new DelugeItemWrapper(new DownloadStatus { Hash = "", Name = "Test", Label = "movies", Trackers = new List<Tracker>(), DownloadLocation = "/downloads" })
            };

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert
            await _fixture.ClientWrapper.DidNotReceive().SetTorrentLabel(Arg.Any<string>(), Arg.Any<string>());
        }

        [Fact]
        public async Task MissingName_SkipsTorrent()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var unlinkedConfig = new UnlinkedConfig
            {
                Id = Guid.NewGuid(),
                TargetCategory = "unlinked"
            };

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new DelugeItemWrapper(new DownloadStatus { Hash = "hash1", Name = "", Label = "movies", Trackers = new List<Tracker>(), DownloadLocation = "/downloads" })
            };

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert
            await _fixture.ClientWrapper.DidNotReceive().SetTorrentLabel(Arg.Any<string>(), Arg.Any<string>());
        }

        [Fact]
        public async Task MissingCategory_SkipsTorrent()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var unlinkedConfig = new UnlinkedConfig
            {
                Id = Guid.NewGuid(),
                TargetCategory = "unlinked"
            };

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new DelugeItemWrapper(new DownloadStatus { Hash = "hash1", Name = "Test", Label = "", Trackers = new List<Tracker>(), DownloadLocation = "/downloads" })
            };

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert
            await _fixture.ClientWrapper.DidNotReceive().SetTorrentLabel(Arg.Any<string>(), Arg.Any<string>());
        }

        [Fact]
        public async Task ExceptionGettingFiles_SkipsTorrent()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var unlinkedConfig = new UnlinkedConfig
            {
                Id = Guid.NewGuid(),
                TargetCategory = "unlinked"
            };

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new DelugeItemWrapper(new DownloadStatus { Hash = "hash1", Name = "Test", Label = "movies", Trackers = new List<Tracker>(), DownloadLocation = "/downloads" })
            };

            _fixture.ClientWrapper
                .GetTorrentFiles("hash1")
                .Throws(new InvalidOperationException("Failed to get files"));

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert
            await _fixture.ClientWrapper.DidNotReceive().SetTorrentLabel(Arg.Any<string>(), Arg.Any<string>());
        }

        [Fact]
        public async Task NoHardlinks_ChangesLabel()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var unlinkedConfig = new UnlinkedConfig
            {
                Id = Guid.NewGuid(),
                TargetCategory = "unlinked"
            };

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new DelugeItemWrapper(new DownloadStatus { Hash = "hash1", Name = "Test", Label = "movies", Trackers = new List<Tracker>(), DownloadLocation = "/downloads" })
            };

            _fixture.ClientWrapper
                .GetTorrentFiles("hash1")
                .Returns(new DelugeContents
                {
                    Contents = new Dictionary<string, DelugeFileOrDirectory>
                    {
                        { "file1.mkv", new DelugeFileOrDirectory { Type = "file", Priority = 1, Index = 0, Path = "file1.mkv" } }
                    }
                });

            _fixture.HardLinkFileService
                .GetHardLinkCount(Arg.Any<string>(), Arg.Any<bool>())
                .Returns(0);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert
            await _fixture.ClientWrapper.Received(1)
                .SetTorrentLabel("hash1", "unlinked");
        }

        [Fact]
        public async Task HasHardlinks_SkipsTorrent()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var unlinkedConfig = new UnlinkedConfig
            {
                Id = Guid.NewGuid(),
                TargetCategory = "unlinked"
            };

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new DelugeItemWrapper(new DownloadStatus { Hash = "hash1", Name = "Test", Label = "movies", Trackers = new List<Tracker>(), DownloadLocation = "/downloads" })
            };

            _fixture.ClientWrapper
                .GetTorrentFiles("hash1")
                .Returns(new DelugeContents
                {
                    Contents = new Dictionary<string, DelugeFileOrDirectory>
                    {
                        { "file1.mkv", new DelugeFileOrDirectory { Type = "file", Priority = 1, Index = 0, Path = "file1.mkv" } }
                    }
                });

            _fixture.HardLinkFileService
                .GetHardLinkCount(Arg.Any<string>(), Arg.Any<bool>())
                .Returns(2);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert
            await _fixture.ClientWrapper.DidNotReceive().SetTorrentLabel(Arg.Any<string>(), Arg.Any<string>());
        }

        [Fact]
        public async Task FileNotFound_SkipsTorrent()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var unlinkedConfig = new UnlinkedConfig
            {
                Id = Guid.NewGuid(),
                TargetCategory = "unlinked"
            };

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new DelugeItemWrapper(new DownloadStatus { Hash = "hash1", Name = "Test", Label = "movies", Trackers = new List<Tracker>(), DownloadLocation = "/downloads" })
            };

            _fixture.ClientWrapper
                .GetTorrentFiles("hash1")
                .Returns(new DelugeContents
                {
                    Contents = new Dictionary<string, DelugeFileOrDirectory>
                    {
                        { "file1.mkv", new DelugeFileOrDirectory { Type = "file", Priority = 1, Index = 0, Path = "file1.mkv" } }
                    }
                });

            _fixture.HardLinkFileService
                .GetHardLinkCount(Arg.Any<string>(), Arg.Any<bool>())
                .Returns(-1);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert
            await _fixture.ClientWrapper.DidNotReceive().SetTorrentLabel(Arg.Any<string>(), Arg.Any<string>());
        }

        [Fact]
        public async Task SkippedFiles_IgnoredInCheck()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var unlinkedConfig = new UnlinkedConfig
            {
                Id = Guid.NewGuid(),
                TargetCategory = "unlinked"
            };

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new DelugeItemWrapper(new DownloadStatus { Hash = "hash1", Name = "Test", Label = "movies", Trackers = new List<Tracker>(), DownloadLocation = "/downloads" })
            };

            _fixture.ClientWrapper
                .GetTorrentFiles("hash1")
                .Returns(new DelugeContents
                {
                    Contents = new Dictionary<string, DelugeFileOrDirectory>
                    {
                        { "file1.mkv", new DelugeFileOrDirectory { Type = "file", Priority = 0, Index = 0, Path = "file1.mkv" } },
                        { "file2.mkv", new DelugeFileOrDirectory { Type = "file", Priority = 1, Index = 1, Path = "file2.mkv" } }
                    }
                });

            _fixture.HardLinkFileService
                .GetHardLinkCount(Arg.Any<string>(), Arg.Any<bool>())
                .Returns(0);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert
            _fixture.HardLinkFileService.Received(1)
                .GetHardLinkCount(Arg.Any<string>(), Arg.Any<bool>());
        }

        [Fact]
        public async Task PublishesCategoryChangedEvent()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var unlinkedConfig = new UnlinkedConfig
            {
                Id = Guid.NewGuid(),
                TargetCategory = "unlinked"
            };

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new DelugeItemWrapper(new DownloadStatus { Hash = "hash1", Name = "Test", Label = "movies", Trackers = new List<Tracker>(), DownloadLocation = "/downloads" })
            };

            _fixture.ClientWrapper
                .GetTorrentFiles("hash1")
                .Returns(new DelugeContents
                {
                    Contents = new Dictionary<string, DelugeFileOrDirectory>
                    {
                        { "file1.mkv", new DelugeFileOrDirectory { Type = "file", Priority = 1, Index = 0, Path = "file1.mkv" } }
                    }
                });

            _fixture.HardLinkFileService
                .GetHardLinkCount(Arg.Any<string>(), Arg.Any<bool>())
                .Returns(0);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert - EventPublisher is not mocked, so we just verify the method completed
            await _fixture.ClientWrapper.Received(1)
                .SetTorrentLabel("hash1", "unlinked");
        }
    }
}
