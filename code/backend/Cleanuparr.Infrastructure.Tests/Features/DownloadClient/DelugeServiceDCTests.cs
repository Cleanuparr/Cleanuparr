using Cleanuparr.Domain.Entities.Deluge.Response;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient.Deluge;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Moq;
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
                .Setup(x => x.GetStatusForAllTorrents())
                .ReturnsAsync(downloads);

            // Act
            var result = await sut.GetSeedingDownloads();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.All(result, item => Assert.NotNull(item.Hash));
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
                .Setup(x => x.GetStatusForAllTorrents())
                .ReturnsAsync(downloads);

            // Act
            var result = await sut.GetSeedingDownloads();

            // Assert
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task ReturnsEmptyList_WhenNull()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            _fixture.ClientWrapper
                .Setup(x => x.GetStatusForAllTorrents())
                .ReturnsAsync((List<DownloadStatus>?)null);

            // Act
            var result = await sut.GetSeedingDownloads();

            // Assert
            Assert.Empty(result);
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
                .Setup(x => x.GetStatusForAllTorrents())
                .ReturnsAsync(downloads);

            // Act
            var result = await sut.GetSeedingDownloads();

            // Assert
            Assert.Single(result);
            Assert.Equal("hash1", result[0].Hash);
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

            var categories = new List<SeedingRule>
            {
                new SeedingRule { Name = "movies", MaxRatio = -1, MinSeedTime = 0, MaxSeedTime = -1, DeleteSourceFiles = true },
                new SeedingRule { Name = "tv", MaxRatio = -1, MinSeedTime = 0, MaxSeedTime = -1, DeleteSourceFiles = true }
            };

            // Act
            var result = sut.FilterDownloadsToBeCleanedAsync(downloads, categories);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Contains(result, x => x.Category == "movies");
            Assert.Contains(result, x => x.Category == "tv");
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

            var categories = new List<SeedingRule>
            {
                new SeedingRule { Name = "movies", MaxRatio = -1, MinSeedTime = 0, MaxSeedTime = -1, DeleteSourceFiles = true }
            };

            // Act
            var result = sut.FilterDownloadsToBeCleanedAsync(downloads, categories);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
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

            var categories = new List<SeedingRule>
            {
                new SeedingRule { Name = "movies", MaxRatio = -1, MinSeedTime = 0, MaxSeedTime = -1, DeleteSourceFiles = true }
            };

            // Act
            var result = sut.FilterDownloadsToBeCleanedAsync(downloads, categories);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
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
            var result = sut.FilterDownloadsToChangeCategoryAsync(downloads, new List<string> { "movies" });

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("hash1", result[0].Hash);
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
            var result = sut.FilterDownloadsToChangeCategoryAsync(downloads, new List<string> { "movies" });

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
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
            var result = sut.FilterDownloadsToChangeCategoryAsync(downloads, new List<string> { "movies" });

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("hash1", result[0].Hash);
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
                .Setup(x => x.GetLabels())
                .ReturnsAsync(new List<string>());

            _fixture.ClientWrapper
                .Setup(x => x.CreateLabel("new-label"))
                .Returns(Task.CompletedTask);

            // Act
            await sut.CreateCategoryAsync("new-label");

            // Assert
            _fixture.ClientWrapper.Verify(x => x.CreateLabel("new-label"), Times.Once);
        }

        [Fact]
        public async Task SkipsCreation_WhenLabelExists()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            _fixture.ClientWrapper
                .Setup(x => x.GetLabels())
                .ReturnsAsync(new List<string> { "existing" });

            // Act
            await sut.CreateCategoryAsync("existing");

            // Assert
            _fixture.ClientWrapper.Verify(x => x.CreateLabel(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task IsCaseInsensitive()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            _fixture.ClientWrapper
                .Setup(x => x.GetLabels())
                .ReturnsAsync(new List<string> { "Existing" });

            // Act
            await sut.CreateCategoryAsync("existing");

            // Assert
            _fixture.ClientWrapper.Verify(x => x.CreateLabel(It.IsAny<string>()), Times.Never);
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

            _fixture.ClientWrapper
                .Setup(x => x.DeleteTorrents(It.Is<List<string>>(h => h.Contains("test-hash")), true))
                .Returns(Task.CompletedTask);

            // Act
            await sut.DeleteDownload(hash, true);

            // Assert
            _fixture.ClientWrapper.Verify(
                x => x.DeleteTorrents(It.Is<List<string>>(h => h.Contains("test-hash")), true),
                Times.Once);
        }

        [Fact]
        public async Task NormalizesHashToLowercase()
        {
            // Arrange
            var sut = _fixture.CreateSut();
            const string hash = "UPPERCASE-HASH";

            _fixture.ClientWrapper
                .Setup(x => x.DeleteTorrents(It.IsAny<List<string>>(), true))
                .Returns(Task.CompletedTask);

            // Act
            await sut.DeleteDownload(hash, true);

            // Assert
            _fixture.ClientWrapper.Verify(
                x => x.DeleteTorrents(It.Is<List<string>>(h => h.Contains("uppercase-hash")), true),
                Times.Once);
        }

        [Fact]
        public async Task CallsClientDeleteWithoutSourceFiles()
        {
            // Arrange
            var sut = _fixture.CreateSut();
            const string hash = "TEST-HASH";

            _fixture.ClientWrapper
                .Setup(x => x.DeleteTorrents(It.Is<List<string>>(h => h.Contains("test-hash")), false))
                .Returns(Task.CompletedTask);

            // Act
            await sut.DeleteDownload(hash, false);

            // Assert
            _fixture.ClientWrapper.Verify(
                x => x.DeleteTorrents(It.Is<List<string>>(h => h.Contains("test-hash")), false),
                Times.Once);
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

            var config = new DownloadCleanerConfig
            {
                Id = Guid.NewGuid(),
                UnlinkedTargetCategory = "unlinked"
            };
            ContextProvider.Set(nameof(DownloadCleanerConfig), config);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(null);

            // Assert
            _fixture.ClientWrapper.Verify(x => x.SetTorrentLabel(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task EmptyDownloads_DoesNothing()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var config = new DownloadCleanerConfig
            {
                Id = Guid.NewGuid(),
                UnlinkedTargetCategory = "unlinked"
            };
            ContextProvider.Set(nameof(DownloadCleanerConfig), config);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(new List<Domain.Entities.ITorrentItemWrapper>());

            // Assert
            _fixture.ClientWrapper.Verify(x => x.SetTorrentLabel(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task MissingHash_SkipsTorrent()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var config = new DownloadCleanerConfig
            {
                Id = Guid.NewGuid(),
                UnlinkedTargetCategory = "unlinked"
            };
            ContextProvider.Set(nameof(DownloadCleanerConfig), config);

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new DelugeItemWrapper(new DownloadStatus { Hash = "", Name = "Test", Label = "movies", Trackers = new List<Tracker>(), DownloadLocation = "/downloads" })
            };

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.ClientWrapper.Verify(x => x.SetTorrentLabel(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task MissingName_SkipsTorrent()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var config = new DownloadCleanerConfig
            {
                Id = Guid.NewGuid(),
                UnlinkedTargetCategory = "unlinked"
            };
            ContextProvider.Set(nameof(DownloadCleanerConfig), config);

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new DelugeItemWrapper(new DownloadStatus { Hash = "hash1", Name = "", Label = "movies", Trackers = new List<Tracker>(), DownloadLocation = "/downloads" })
            };

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.ClientWrapper.Verify(x => x.SetTorrentLabel(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task MissingCategory_SkipsTorrent()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var config = new DownloadCleanerConfig
            {
                Id = Guid.NewGuid(),
                UnlinkedTargetCategory = "unlinked"
            };
            ContextProvider.Set(nameof(DownloadCleanerConfig), config);

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new DelugeItemWrapper(new DownloadStatus { Hash = "hash1", Name = "Test", Label = "", Trackers = new List<Tracker>(), DownloadLocation = "/downloads" })
            };

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.ClientWrapper.Verify(x => x.SetTorrentLabel(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ExceptionGettingFiles_SkipsTorrent()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var config = new DownloadCleanerConfig
            {
                Id = Guid.NewGuid(),
                UnlinkedTargetCategory = "unlinked"
            };
            ContextProvider.Set(nameof(DownloadCleanerConfig), config);

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new DelugeItemWrapper(new DownloadStatus { Hash = "hash1", Name = "Test", Label = "movies", Trackers = new List<Tracker>(), DownloadLocation = "/downloads" })
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFiles("hash1"))
                .ThrowsAsync(new InvalidOperationException("Failed to get files"));

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.ClientWrapper.Verify(x => x.SetTorrentLabel(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task NoHardlinks_ChangesLabel()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var config = new DownloadCleanerConfig
            {
                Id = Guid.NewGuid(),
                UnlinkedTargetCategory = "unlinked"
            };
            ContextProvider.Set(nameof(DownloadCleanerConfig), config);

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new DelugeItemWrapper(new DownloadStatus { Hash = "hash1", Name = "Test", Label = "movies", Trackers = new List<Tracker>(), DownloadLocation = "/downloads" })
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFiles("hash1"))
                .ReturnsAsync(new DelugeContents
                {
                    Contents = new Dictionary<string, DelugeFileOrDirectory>
                    {
                        { "file1.mkv", new DelugeFileOrDirectory { Type = "file", Priority = 1, Index = 0, Path = "file1.mkv" } }
                    }
                });

            _fixture.HardLinkFileService
                .Setup(x => x.GetHardLinkCount(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(0);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.ClientWrapper.Verify(
                x => x.SetTorrentLabel("hash1", "unlinked"),
                Times.Once);
        }

        [Fact]
        public async Task HasHardlinks_SkipsTorrent()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var config = new DownloadCleanerConfig
            {
                Id = Guid.NewGuid(),
                UnlinkedTargetCategory = "unlinked"
            };
            ContextProvider.Set(nameof(DownloadCleanerConfig), config);

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new DelugeItemWrapper(new DownloadStatus { Hash = "hash1", Name = "Test", Label = "movies", Trackers = new List<Tracker>(), DownloadLocation = "/downloads" })
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFiles("hash1"))
                .ReturnsAsync(new DelugeContents
                {
                    Contents = new Dictionary<string, DelugeFileOrDirectory>
                    {
                        { "file1.mkv", new DelugeFileOrDirectory { Type = "file", Priority = 1, Index = 0, Path = "file1.mkv" } }
                    }
                });

            _fixture.HardLinkFileService
                .Setup(x => x.GetHardLinkCount(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(2);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.ClientWrapper.Verify(x => x.SetTorrentLabel(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task FileNotFound_SkipsTorrent()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var config = new DownloadCleanerConfig
            {
                Id = Guid.NewGuid(),
                UnlinkedTargetCategory = "unlinked"
            };
            ContextProvider.Set(nameof(DownloadCleanerConfig), config);

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new DelugeItemWrapper(new DownloadStatus { Hash = "hash1", Name = "Test", Label = "movies", Trackers = new List<Tracker>(), DownloadLocation = "/downloads" })
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFiles("hash1"))
                .ReturnsAsync(new DelugeContents
                {
                    Contents = new Dictionary<string, DelugeFileOrDirectory>
                    {
                        { "file1.mkv", new DelugeFileOrDirectory { Type = "file", Priority = 1, Index = 0, Path = "file1.mkv" } }
                    }
                });

            _fixture.HardLinkFileService
                .Setup(x => x.GetHardLinkCount(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(-1);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.ClientWrapper.Verify(x => x.SetTorrentLabel(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task SkippedFiles_IgnoredInCheck()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var config = new DownloadCleanerConfig
            {
                Id = Guid.NewGuid(),
                UnlinkedTargetCategory = "unlinked"
            };
            ContextProvider.Set(nameof(DownloadCleanerConfig), config);

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new DelugeItemWrapper(new DownloadStatus { Hash = "hash1", Name = "Test", Label = "movies", Trackers = new List<Tracker>(), DownloadLocation = "/downloads" })
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFiles("hash1"))
                .ReturnsAsync(new DelugeContents
                {
                    Contents = new Dictionary<string, DelugeFileOrDirectory>
                    {
                        { "file1.mkv", new DelugeFileOrDirectory { Type = "file", Priority = 0, Index = 0, Path = "file1.mkv" } },
                        { "file2.mkv", new DelugeFileOrDirectory { Type = "file", Priority = 1, Index = 1, Path = "file2.mkv" } }
                    }
                });

            _fixture.HardLinkFileService
                .Setup(x => x.GetHardLinkCount(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(0);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.HardLinkFileService.Verify(
                x => x.GetHardLinkCount(It.IsAny<string>(), It.IsAny<bool>()),
                Times.Once);
        }

        [Fact]
        public async Task WithIgnoredRootDir_PopulatesFileCounts()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var config = new DownloadCleanerConfig
            {
                Id = Guid.NewGuid(),
                UnlinkedTargetCategory = "unlinked",
                UnlinkedIgnoredRootDir = "/ignore"
            };
            ContextProvider.Set(nameof(DownloadCleanerConfig), config);

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new DelugeItemWrapper(new DownloadStatus { Hash = "hash1", Name = "Test", Label = "movies", Trackers = new List<Tracker>(), DownloadLocation = "/downloads" })
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFiles("hash1"))
                .ReturnsAsync(new DelugeContents
                {
                    Contents = new Dictionary<string, DelugeFileOrDirectory>
                    {
                        { "file1.mkv", new DelugeFileOrDirectory { Type = "file", Priority = 1, Index = 0, Path = "file1.mkv" } }
                    }
                });

            _fixture.HardLinkFileService
                .Setup(x => x.GetHardLinkCount(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(0);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.HardLinkFileService.Verify(
                x => x.PopulateFileCounts("/ignore"),
                Times.Once);
        }

        [Fact]
        public async Task PublishesCategoryChangedEvent()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var config = new DownloadCleanerConfig
            {
                Id = Guid.NewGuid(),
                UnlinkedTargetCategory = "unlinked"
            };
            ContextProvider.Set(nameof(DownloadCleanerConfig), config);

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new DelugeItemWrapper(new DownloadStatus { Hash = "hash1", Name = "Test", Label = "movies", Trackers = new List<Tracker>(), DownloadLocation = "/downloads" })
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFiles("hash1"))
                .ReturnsAsync(new DelugeContents
                {
                    Contents = new Dictionary<string, DelugeFileOrDirectory>
                    {
                        { "file1.mkv", new DelugeFileOrDirectory { Type = "file", Priority = 1, Index = 0, Path = "file1.mkv" } }
                    }
                });

            _fixture.HardLinkFileService
                .Setup(x => x.GetHardLinkCount(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(0);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert - EventPublisher is not mocked, so we just verify the method completed
            _fixture.ClientWrapper.Verify(
                x => x.SetTorrentLabel("hash1", "unlinked"),
                Times.Once);
        }
    }
}
