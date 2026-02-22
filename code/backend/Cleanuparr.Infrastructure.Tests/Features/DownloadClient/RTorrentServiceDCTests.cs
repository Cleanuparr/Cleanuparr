using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.RTorrent.Response;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient.RTorrent;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Moq;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient;

public class RTorrentServiceDCTests : IClassFixture<RTorrentServiceFixture>
{
    private readonly RTorrentServiceFixture _fixture;

    public RTorrentServiceDCTests(RTorrentServiceFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetMocks();
    }

    public class GetSeedingDownloads_Tests : RTorrentServiceDCTests
    {
        public GetSeedingDownloads_Tests(RTorrentServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task FiltersSeedingState()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var downloads = new List<RTorrentTorrent>
            {
                new RTorrentTorrent { Hash = "HASH1", Name = "Torrent 1", State = 1, Complete = 1, IsPrivate = 0, Label = "" },
                new RTorrentTorrent { Hash = "HASH2", Name = "Torrent 2", State = 1, Complete = 0, IsPrivate = 0, Label = "" }, // Downloading, not seeding
                new RTorrentTorrent { Hash = "HASH3", Name = "Torrent 3", State = 1, Complete = 1, IsPrivate = 0, Label = "" },
                new RTorrentTorrent { Hash = "HASH4", Name = "Torrent 4", State = 0, Complete = 1, IsPrivate = 0, Label = "" } // Stopped, not seeding
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetAllTorrentsAsync())
                .ReturnsAsync(downloads);

            // Act
            var result = await sut.GetSeedingDownloads();

            // Assert - only torrents with State=1 AND Complete=1 should be returned
            Assert.Equal(2, result.Count);
            Assert.All(result, item => Assert.NotNull(item.Hash));
        }

        [Fact]
        public async Task ReturnsEmptyList_WhenNoTorrents()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            _fixture.ClientWrapper
                .Setup(x => x.GetAllTorrentsAsync())
                .ReturnsAsync(new List<RTorrentTorrent>());

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

            var downloads = new List<RTorrentTorrent>
            {
                new RTorrentTorrent { Hash = "", Name = "No Hash", State = 1, Complete = 1, IsPrivate = 0, Label = "" },
                new RTorrentTorrent { Hash = "HASH1", Name = "Valid Hash", State = 1, Complete = 1, IsPrivate = 0, Label = "" }
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetAllTorrentsAsync())
                .ReturnsAsync(downloads);

            // Act
            var result = await sut.GetSeedingDownloads();

            // Assert
            Assert.Single(result);
            Assert.Equal("HASH1", result[0].Hash);
        }
    }

    public class FilterDownloadsToBeCleanedAsync_Tests : RTorrentServiceDCTests
    {
        public FilterDownloadsToBeCleanedAsync_Tests(RTorrentServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public void MatchesCategories()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var downloads = new List<ITorrentItemWrapper>
            {
                new RTorrentItemWrapper(new RTorrentTorrent { Hash = "HASH1", Name = "Torrent 1", Label = "movies" }),
                new RTorrentItemWrapper(new RTorrentTorrent { Hash = "HASH2", Name = "Torrent 2", Label = "tv" }),
                new RTorrentItemWrapper(new RTorrentTorrent { Hash = "HASH3", Name = "Torrent 3", Label = "music" })
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

            var downloads = new List<ITorrentItemWrapper>
            {
                new RTorrentItemWrapper(new RTorrentTorrent { Hash = "HASH1", Name = "Torrent 1", Label = "Movies" })
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

            var downloads = new List<ITorrentItemWrapper>
            {
                new RTorrentItemWrapper(new RTorrentTorrent { Hash = "HASH1", Name = "Torrent 1", Label = "music" })
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

        [Fact]
        public void ReturnsNull_WhenDownloadsNull()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var categories = new List<SeedingRule>
            {
                new SeedingRule { Name = "movies", MaxRatio = -1, MinSeedTime = 0, MaxSeedTime = -1, DeleteSourceFiles = true }
            };

            // Act
            var result = sut.FilterDownloadsToBeCleanedAsync(null, categories);

            // Assert
            Assert.Null(result);
        }
    }

    public class FilterDownloadsToChangeCategoryAsync_Tests : RTorrentServiceDCTests
    {
        public FilterDownloadsToChangeCategoryAsync_Tests(RTorrentServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public void MatchesCategories()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var downloads = new List<ITorrentItemWrapper>
            {
                new RTorrentItemWrapper(new RTorrentTorrent { Hash = "HASH1", Name = "Torrent 1", Label = "movies" }),
                new RTorrentItemWrapper(new RTorrentTorrent { Hash = "HASH2", Name = "Torrent 2", Label = "tv" }),
                new RTorrentItemWrapper(new RTorrentTorrent { Hash = "HASH3", Name = "Torrent 3", Label = "music" })
            };

            var categories = new List<string> { "movies", "tv" };

            // Act
            var result = sut.FilterDownloadsToChangeCategoryAsync(downloads, categories);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void SkipsEmptyHashes()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var downloads = new List<ITorrentItemWrapper>
            {
                new RTorrentItemWrapper(new RTorrentTorrent { Hash = "", Name = "No Hash", Label = "movies" }),
                new RTorrentItemWrapper(new RTorrentTorrent { Hash = "HASH1", Name = "Valid Hash", Label = "movies" })
            };

            var categories = new List<string> { "movies" };

            // Act
            var result = sut.FilterDownloadsToChangeCategoryAsync(downloads, categories);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("HASH1", result[0].Hash);
        }
    }

    public class DeleteDownload_Tests : RTorrentServiceDCTests
    {
        public DeleteDownload_Tests(RTorrentServiceFixture fixture) : base(fixture)
        {
        }


        [Fact]
        public async Task NormalizesHashToUppercase()
        {
            // Arrange
            var sut = _fixture.CreateSut();
            var hash = "lowercase";
            var mockTorrent = new Mock<ITorrentItemWrapper>();
            mockTorrent.Setup(x => x.Hash).Returns(hash);
            mockTorrent.Setup(x => x.SavePath).Returns("/test/path");

            _fixture.ClientWrapper
                .Setup(x => x.DeleteTorrentAsync("LOWERCASE"))
                .Returns(Task.CompletedTask);

            // Act
            await sut.DeleteDownload(mockTorrent.Object, deleteSourceFiles: false);

            // Assert
            _fixture.ClientWrapper.Verify(
                x => x.DeleteTorrentAsync("LOWERCASE"),
                Times.Once);
        }
    }

    public class CreateCategoryAsync_Tests : RTorrentServiceDCTests
    {
        public CreateCategoryAsync_Tests(RTorrentServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task IsNoOp_BecauseRTorrentDoesNotSupportCategories()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            // Act
            await sut.CreateCategoryAsync("test-category");

            // Assert - no client calls should be made
            _fixture.ClientWrapper.VerifyNoOtherCalls();
        }
    }

    public class ChangeCategoryForNoHardLinksAsync_Tests : RTorrentServiceDCTests
    {
        public ChangeCategoryForNoHardLinksAsync_Tests(RTorrentServiceFixture fixture) : base(fixture)
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
            _fixture.ClientWrapper.Verify(
                x => x.SetLabelAsync(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
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
            await sut.ChangeCategoryForNoHardLinksAsync(new List<ITorrentItemWrapper>());

            // Assert
            _fixture.ClientWrapper.Verify(
                x => x.SetLabelAsync(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
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

            var downloads = new List<ITorrentItemWrapper>
            {
                new RTorrentItemWrapper(new RTorrentTorrent { Hash = "", Name = "Test", Label = "movies", BasePath = "/downloads" })
            };

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.ClientWrapper.Verify(
                x => x.SetLabelAsync(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
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

            var downloads = new List<ITorrentItemWrapper>
            {
                new RTorrentItemWrapper(new RTorrentTorrent { Hash = "HASH1", Name = "", Label = "movies", BasePath = "/downloads" })
            };

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.ClientWrapper.Verify(
                x => x.SetLabelAsync(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
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

            var downloads = new List<ITorrentItemWrapper>
            {
                new RTorrentItemWrapper(new RTorrentTorrent { Hash = "HASH1", Name = "Test", Label = "", BasePath = "/downloads" })
            };

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.ClientWrapper.Verify(
                x => x.SetLabelAsync(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task GetFilesThrows_SkipsTorrent()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var config = new DownloadCleanerConfig
            {
                Id = Guid.NewGuid(),
                UnlinkedTargetCategory = "unlinked"
            };
            ContextProvider.Set(nameof(DownloadCleanerConfig), config);

            var downloads = new List<ITorrentItemWrapper>
            {
                new RTorrentItemWrapper(new RTorrentTorrent { Hash = "HASH1", Name = "Test", Label = "movies", BasePath = "/downloads" })
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFilesAsync("HASH1"))
                .ThrowsAsync(new Exception("XML-RPC error"));

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.ClientWrapper.Verify(
                x => x.SetLabelAsync(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
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

            var downloads = new List<ITorrentItemWrapper>
            {
                new RTorrentItemWrapper(new RTorrentTorrent { Hash = "HASH1", Name = "Test", Label = "movies", BasePath = "/downloads" })
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFilesAsync("HASH1"))
                .ReturnsAsync(new List<RTorrentFile>
                {
                    new RTorrentFile { Index = 0, Path = "file1.mkv", Priority = 0 }, // Skipped
                    new RTorrentFile { Index = 1, Path = "file2.mkv", Priority = 1 }  // Active
                });

            _fixture.HardLinkFileService
                .Setup(x => x.GetHardLinkCount(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(0);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert - only called for file2.mkv (the active file)
            _fixture.HardLinkFileService.Verify(
                x => x.GetHardLinkCount(It.IsAny<string>(), It.IsAny<bool>()),
                Times.Once);
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

            var downloads = new List<ITorrentItemWrapper>
            {
                new RTorrentItemWrapper(new RTorrentTorrent { Hash = "HASH1", Name = "Test", Label = "movies", BasePath = "/downloads" })
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFilesAsync("HASH1"))
                .ReturnsAsync(new List<RTorrentFile>
                {
                    new RTorrentFile { Index = 0, Path = "file1.mkv", Priority = 1 }
                });

            _fixture.HardLinkFileService
                .Setup(x => x.GetHardLinkCount(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(0);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert - rTorrent uses SetLabelAsync (not SetTorrentCategoryAsync)
            _fixture.ClientWrapper.Verify(
                x => x.SetLabelAsync("HASH1", "unlinked"),
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

            var downloads = new List<ITorrentItemWrapper>
            {
                new RTorrentItemWrapper(new RTorrentTorrent { Hash = "HASH1", Name = "Test", Label = "movies", BasePath = "/downloads" })
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFilesAsync("HASH1"))
                .ReturnsAsync(new List<RTorrentFile>
                {
                    new RTorrentFile { Index = 0, Path = "file1.mkv", Priority = 1 }
                });

            _fixture.HardLinkFileService
                .Setup(x => x.GetHardLinkCount(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(2); // Has hardlinks

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.ClientWrapper.Verify(
                x => x.SetLabelAsync(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
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

            var downloads = new List<ITorrentItemWrapper>
            {
                new RTorrentItemWrapper(new RTorrentTorrent { Hash = "HASH1", Name = "Test", Label = "movies", BasePath = "/downloads" })
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFilesAsync("HASH1"))
                .ReturnsAsync(new List<RTorrentFile>
                {
                    new RTorrentFile { Index = 0, Path = "file1.mkv", Priority = 1 }
                });

            _fixture.HardLinkFileService
                .Setup(x => x.GetHardLinkCount(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(-1); // Error / file not found

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.ClientWrapper.Verify(
                x => x.SetLabelAsync(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
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

            var downloads = new List<ITorrentItemWrapper>
            {
                new RTorrentItemWrapper(new RTorrentTorrent { Hash = "HASH1", Name = "Test", Label = "movies", BasePath = "/downloads" })
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFilesAsync("HASH1"))
                .ReturnsAsync(new List<RTorrentFile>
                {
                    new RTorrentFile { Index = 0, Path = "file1.mkv", Priority = 1 }
                });

            _fixture.HardLinkFileService
                .Setup(x => x.GetHardLinkCount(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(0);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.EventPublisher.Verify(
                x => x.PublishCategoryChanged("movies", "unlinked", false),
                Times.Once);
        }

        [Fact]
        public async Task UpdatesCategoryOnWrapper()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var config = new DownloadCleanerConfig
            {
                Id = Guid.NewGuid(),
                UnlinkedTargetCategory = "unlinked"
            };
            ContextProvider.Set(nameof(DownloadCleanerConfig), config);

            var wrapper = new RTorrentItemWrapper(new RTorrentTorrent { Hash = "HASH1", Name = "Test", Label = "movies", BasePath = "/downloads" });
            var downloads = new List<ITorrentItemWrapper> { wrapper };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentFilesAsync("HASH1"))
                .ReturnsAsync(new List<RTorrentFile>
                {
                    new RTorrentFile { Index = 0, Path = "file1.mkv", Priority = 1 }
                });

            _fixture.HardLinkFileService
                .Setup(x => x.GetHardLinkCount(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(0);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            Assert.Equal("unlinked", wrapper.Category);
        }
    }
}
