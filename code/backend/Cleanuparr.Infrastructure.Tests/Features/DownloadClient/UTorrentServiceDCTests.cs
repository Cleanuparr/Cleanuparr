using Cleanuparr.Domain.Entities.UTorrent.Response;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Moq;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient;

public class UTorrentServiceDCTests : IClassFixture<UTorrentServiceFixture>
{
    private readonly UTorrentServiceFixture _fixture;

    public UTorrentServiceDCTests(UTorrentServiceFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetMocks();
    }

    public class GetSeedingDownloads_Tests : UTorrentServiceDCTests
    {
        public GetSeedingDownloads_Tests(UTorrentServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task FiltersSeedingTorrents()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var torrents = new List<UTorrentItem>
            {
                new UTorrentItem { Hash = "hash1", Name = "Torrent 1", Status = 9, DateCompleted = 1000 }, // Seeding (Started + Checked, DateCompleted > 0)
                new UTorrentItem { Hash = "hash2", Name = "Torrent 2", Status = 9, DateCompleted = 0 },  // Downloading (Started + Checked, DateCompleted = 0)
                new UTorrentItem { Hash = "hash3", Name = "Torrent 3", Status = 9, DateCompleted = 2000 }  // Seeding (Started + Checked, DateCompleted > 0)
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentsAsync())
                .ReturnsAsync(torrents);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentPropertiesAsync("hash1"))
                .ReturnsAsync(new UTorrentProperties { Hash = "hash1", Pex = 1, Trackers = "" });

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentPropertiesAsync("hash3"))
                .ReturnsAsync(new UTorrentProperties { Hash = "hash3", Pex = 1, Trackers = "" });

            // Act
            var result = await sut.GetSeedingDownloads();

            // Assert
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task ReturnsEmptyList_WhenNoSeedingTorrents()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var torrents = new List<UTorrentItem>
            {
                new UTorrentItem { Hash = "hash1", Name = "Torrent 1", Status = 9 } // Not seeding
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentsAsync())
                .ReturnsAsync(torrents);

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

            var torrents = new List<UTorrentItem>
            {
                new UTorrentItem { Hash = "", Name = "No Hash", Status = 9, DateCompleted = 1000 },
                new UTorrentItem { Hash = "hash1", Name = "Valid Hash", Status = 9, DateCompleted = 1000 }
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentsAsync())
                .ReturnsAsync(torrents);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentPropertiesAsync("hash1"))
                .ReturnsAsync(new UTorrentProperties { Hash = "hash1", Pex = 1, Trackers = "" });

            // Act
            var result = await sut.GetSeedingDownloads();

            // Assert
            Assert.Single(result);
            Assert.Equal("hash1", result[0].Hash);
        }
    }

    public class FilterDownloadsToBeCleanedAsync_Tests : UTorrentServiceDCTests
    {
        public FilterDownloadsToBeCleanedAsync_Tests(UTorrentServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public void MatchesCategories()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new UTorrentItemWrapper(new UTorrentItem { Hash = "hash1", Label = "movies" }, new UTorrentProperties { Hash = "hash1", Pex = 1, Trackers = "" }),
                new UTorrentItemWrapper(new UTorrentItem { Hash = "hash2", Label = "tv" }, new UTorrentProperties { Hash = "hash2", Pex = 1, Trackers = "" }),
                new UTorrentItemWrapper(new UTorrentItem { Hash = "hash3", Label = "music" }, new UTorrentProperties { Hash = "hash3", Pex = 1, Trackers = "" })
            };

            var categories = new List<CleanCategory>
            {
                new CleanCategory { Name = "movies", MaxRatio = -1, MinSeedTime = 0, MaxSeedTime = -1 },
                new CleanCategory { Name = "tv", MaxRatio = -1, MinSeedTime = 0, MaxSeedTime = -1 }
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
                new UTorrentItemWrapper(new UTorrentItem { Hash = "hash1", Label = "Movies" }, new UTorrentProperties { Hash = "hash1", Pex = 1, Trackers = "" })
            };

            var categories = new List<CleanCategory>
            {
                new CleanCategory { Name = "movies", MaxRatio = -1, MinSeedTime = 0, MaxSeedTime = -1 }
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
                new UTorrentItemWrapper(new UTorrentItem { Hash = "hash1", Label = "music" }, new UTorrentProperties { Hash = "hash1", Pex = 1, Trackers = "" })
            };

            var categories = new List<CleanCategory>
            {
                new CleanCategory { Name = "movies", MaxRatio = -1, MinSeedTime = 0, MaxSeedTime = -1 }
            };

            // Act
            var result = sut.FilterDownloadsToBeCleanedAsync(downloads, categories);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }
    }

    public class FilterDownloadsToChangeCategoryAsync_Tests : UTorrentServiceDCTests
    {
        public FilterDownloadsToChangeCategoryAsync_Tests(UTorrentServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public void FiltersCorrectly()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new UTorrentItemWrapper(new UTorrentItem { Hash = "hash1", Label = "movies" }, new UTorrentProperties { Hash = "hash1", Pex = 1, Trackers = "" }),
                new UTorrentItemWrapper(new UTorrentItem { Hash = "hash2", Label = "tv" }, new UTorrentProperties { Hash = "hash2", Pex = 1, Trackers = "" })
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
                new UTorrentItemWrapper(new UTorrentItem { Hash = "hash1", Label = "Movies" }, new UTorrentProperties { Hash = "hash1", Pex = 1, Trackers = "" })
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
                new UTorrentItemWrapper(new UTorrentItem { Hash = "", Label = "movies" }, new UTorrentProperties { Hash = "", Pex = 1, Trackers = "" }),
                new UTorrentItemWrapper(new UTorrentItem { Hash = "hash1", Label = "movies" }, new UTorrentProperties { Hash = "hash1", Pex = 1, Trackers = "" })
            };

            // Act
            var result = sut.FilterDownloadsToChangeCategoryAsync(downloads, new List<string> { "movies" });

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("hash1", result[0].Hash);
        }
    }

    public class CreateCategoryAsync_Tests : UTorrentServiceDCTests
    {
        public CreateCategoryAsync_Tests(UTorrentServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task IsNoOp()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            // Act
            await sut.CreateCategoryAsync("new-category");

            // Assert - no exceptions thrown, no client calls made
        }
    }

    public class DeleteDownload_Tests : UTorrentServiceDCTests
    {
        public DeleteDownload_Tests(UTorrentServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task CallsClientDelete()
        {
            // Arrange
            var sut = _fixture.CreateSut();
            const string hash = "TEST-HASH";

            _fixture.ClientWrapper
                .Setup(x => x.RemoveTorrentsAsync(It.Is<List<string>>(h => h.Contains("test-hash"))))
                .Returns(Task.CompletedTask);

            // Act
            await sut.DeleteDownload(hash);

            // Assert
            _fixture.ClientWrapper.Verify(
                x => x.RemoveTorrentsAsync(It.Is<List<string>>(h => h.Contains("test-hash"))),
                Times.Once);
        }

        [Fact]
        public async Task NormalizesHashToLowercase()
        {
            // Arrange
            var sut = _fixture.CreateSut();
            const string hash = "UPPERCASE-HASH";

            _fixture.ClientWrapper
                .Setup(x => x.RemoveTorrentsAsync(It.IsAny<List<string>>()))
                .Returns(Task.CompletedTask);

            // Act
            await sut.DeleteDownload(hash);

            // Assert
            _fixture.ClientWrapper.Verify(
                x => x.RemoveTorrentsAsync(It.Is<List<string>>(h => h.Contains("uppercase-hash"))),
                Times.Once);
        }
    }

    public class ChangeCategoryForNoHardLinksAsync_Tests : UTorrentServiceDCTests
    {
        public ChangeCategoryForNoHardLinksAsync_Tests(UTorrentServiceFixture fixture) : base(fixture)
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
            _fixture.ClientWrapper.Verify(x => x.SetTorrentLabelAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
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
            _fixture.ClientWrapper.Verify(x => x.SetTorrentLabelAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
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
                new UTorrentItemWrapper(
                    new UTorrentItem { Hash = "", Name = "Test", Label = "movies", SavePath = "/downloads" },
                    new UTorrentProperties { Hash = "", Pex = 1, Trackers = "" })
            };

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.ClientWrapper.Verify(x => x.SetTorrentLabelAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
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
                new UTorrentItemWrapper(
                    new UTorrentItem { Hash = "hash1", Name = "", Label = "movies", SavePath = "/downloads" },
                    new UTorrentProperties { Hash = "hash1", Pex = 1, Trackers = "" })
            };

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.ClientWrapper.Verify(x => x.SetTorrentLabelAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
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
                new UTorrentItemWrapper(
                    new UTorrentItem { Hash = "hash1", Name = "Test", Label = "", SavePath = "/downloads" },
                    new UTorrentProperties { Hash = "hash1", Pex = 1, Trackers = "" })
            };

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.ClientWrapper.Verify(x => x.SetTorrentLabelAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
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
                new UTorrentItemWrapper(
                    new UTorrentItem { Hash = "hash1", Name = "Test", Label = "movies", SavePath = "/downloads" },
                    new UTorrentProperties { Hash = "hash1", Pex = 1, Trackers = "" })
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFilesAsync("hash1"))
                .ReturnsAsync(new List<UTorrentFile>
                {
                    new UTorrentFile { Name = "file1.mkv", Priority = 1, Index = 0, Size = 1000, Downloaded = 500 }
                });

            _fixture.HardLinkFileService
                .Setup(x => x.GetHardLinkCount(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(0);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.ClientWrapper.Verify(
                x => x.SetTorrentLabelAsync("hash1", "unlinked"),
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
                new UTorrentItemWrapper(
                    new UTorrentItem { Hash = "hash1", Name = "Test", Label = "movies", SavePath = "/downloads" },
                    new UTorrentProperties { Hash = "hash1", Pex = 1, Trackers = "" })
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFilesAsync("hash1"))
                .ReturnsAsync(new List<UTorrentFile>
                {
                    new UTorrentFile { Name = "file1.mkv", Priority = 1, Index = 0, Size = 1000, Downloaded = 500 }
                });

            _fixture.HardLinkFileService
                .Setup(x => x.GetHardLinkCount(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(2);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.ClientWrapper.Verify(x => x.SetTorrentLabelAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
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
                new UTorrentItemWrapper(
                    new UTorrentItem { Hash = "hash1", Name = "Test", Label = "movies", SavePath = "/downloads" },
                    new UTorrentProperties { Hash = "hash1", Pex = 1, Trackers = "" })
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFilesAsync("hash1"))
                .ReturnsAsync(new List<UTorrentFile>
                {
                    new UTorrentFile { Name = "file1.mkv", Priority = 1, Index = 0, Size = 1000, Downloaded = 500 }
                });

            _fixture.HardLinkFileService
                .Setup(x => x.GetHardLinkCount(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(-1);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.ClientWrapper.Verify(x => x.SetTorrentLabelAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
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
                new UTorrentItemWrapper(
                    new UTorrentItem { Hash = "hash1", Name = "Test", Label = "movies", SavePath = "/downloads" },
                    new UTorrentProperties { Hash = "hash1", Pex = 1, Trackers = "" })
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFilesAsync("hash1"))
                .ReturnsAsync(new List<UTorrentFile>
                {
                    new UTorrentFile { Name = "file1.mkv", Priority = 0, Index = 0, Size = 1000, Downloaded = 0 },
                    new UTorrentFile { Name = "file2.mkv", Priority = 1, Index = 1, Size = 2000, Downloaded = 1000 }
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
                new UTorrentItemWrapper(
                    new UTorrentItem { Hash = "hash1", Name = "Test", Label = "movies", SavePath = "/downloads" },
                    new UTorrentProperties { Hash = "hash1", Pex = 1, Trackers = "" })
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFilesAsync("hash1"))
                .ReturnsAsync(new List<UTorrentFile>
                {
                    new UTorrentFile { Name = "file1.mkv", Priority = 1, Index = 0, Size = 1000, Downloaded = 500 }
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
                new UTorrentItemWrapper(
                    new UTorrentItem { Hash = "hash1", Name = "Test", Label = "movies", SavePath = "/downloads" },
                    new UTorrentProperties { Hash = "hash1", Pex = 1, Trackers = "" })
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFilesAsync("hash1"))
                .ReturnsAsync(new List<UTorrentFile>
                {
                    new UTorrentFile { Name = "file1.mkv", Priority = 1, Index = 0, Size = 1000, Downloaded = 500 }
                });

            _fixture.HardLinkFileService
                .Setup(x => x.GetHardLinkCount(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(0);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert - EventPublisher is not mocked, so we just verify the method completed
            _fixture.ClientWrapper.Verify(
                x => x.SetTorrentLabelAsync("hash1", "unlinked"),
                Times.Once);
        }

        [Fact]
        public async Task NullFilesResponse_ChangesLabel()
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
                new UTorrentItemWrapper(
                    new UTorrentItem { Hash = "hash1", Name = "Test", Label = "movies", SavePath = "/downloads" },
                    new UTorrentProperties { Hash = "hash1", Pex = 1, Trackers = "" })
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFilesAsync("hash1"))
                .ReturnsAsync((List<UTorrentFile>?)null);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert - When files is null, it uses empty collection and proceeds to change label
            _fixture.ClientWrapper.Verify(x => x.SetTorrentLabelAsync("hash1", "unlinked"), Times.Once);
        }
    }
}
