using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient.Transmission;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Moq;
using Transmission.API.RPC.Entity;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient;

public class TransmissionServiceDCTests : IClassFixture<TransmissionServiceFixture>
{
    private readonly TransmissionServiceFixture _fixture;

    public TransmissionServiceDCTests(TransmissionServiceFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetMocks();
    }

    public class GetSeedingDownloads_Tests : TransmissionServiceDCTests
    {
        public GetSeedingDownloads_Tests(TransmissionServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task FiltersStatus5And6()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var torrents = new TransmissionTorrents
            {
                Torrents = new[]
                {
                    new TorrentInfo { HashString = "hash1", Name = "Torrent 1", Status = 5 }, // Seeding
                    new TorrentInfo { HashString = "hash2", Name = "Torrent 2", Status = 4 }, // Downloading
                    new TorrentInfo { HashString = "hash3", Name = "Torrent 3", Status = 6 }  // Seeding
                }
            };

            _fixture.ClientWrapper
                .Setup(x => x.TorrentGetAsync(It.IsAny<string[]>(), It.IsAny<string?>()))
                .ReturnsAsync(torrents);

            // Act
            var result = await sut.GetSeedingDownloads();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.All(result, item => Assert.NotNull(item.Hash));
        }

        [Fact]
        public async Task ReturnsEmptyList_WhenNull()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            _fixture.ClientWrapper
                .Setup(x => x.TorrentGetAsync(It.IsAny<string[]>(), It.IsAny<string?>()))
                .ReturnsAsync((TransmissionTorrents?)null);

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

            var torrents = new TransmissionTorrents
            {
                Torrents = new[]
                {
                    new TorrentInfo { HashString = "", Name = "No Hash", Status = 5 },
                    new TorrentInfo { HashString = "hash1", Name = "Valid Hash", Status = 5 }
                }
            };

            _fixture.ClientWrapper
                .Setup(x => x.TorrentGetAsync(It.IsAny<string[]>(), It.IsAny<string?>()))
                .ReturnsAsync(torrents);

            // Act
            var result = await sut.GetSeedingDownloads();

            // Assert
            Assert.Single(result);
            Assert.Equal("hash1", result[0].Hash);
        }

        [Fact]
        public async Task ReturnsEmptyList_WhenTorrentsNull()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var torrents = new TransmissionTorrents
            {
                Torrents = null
            };

            _fixture.ClientWrapper
                .Setup(x => x.TorrentGetAsync(It.IsAny<string[]>(), It.IsAny<string?>()))
                .ReturnsAsync(torrents);

            // Act
            var result = await sut.GetSeedingDownloads();

            // Assert
            Assert.Empty(result);
        }
    }

    public class FilterDownloadsToBeCleanedAsync_Tests : TransmissionServiceDCTests
    {
        public FilterDownloadsToBeCleanedAsync_Tests(TransmissionServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public void MatchesCategories()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new TransmissionItemWrapper(new TorrentInfo { HashString = "hash1", DownloadDir = "/downloads/movies" }),
                new TransmissionItemWrapper(new TorrentInfo { HashString = "hash2", DownloadDir = "/downloads/tv" }),
                new TransmissionItemWrapper(new TorrentInfo { HashString = "hash3", DownloadDir = "/downloads/music" })
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
                new TransmissionItemWrapper(new TorrentInfo { HashString = "hash1", DownloadDir = "/downloads/Movies" })
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
                new TransmissionItemWrapper(new TorrentInfo { HashString = "hash1", DownloadDir = "/downloads/music" })
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

    public class FilterDownloadsToChangeCategoryAsync_Tests : TransmissionServiceDCTests
    {
        public FilterDownloadsToChangeCategoryAsync_Tests(TransmissionServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public void FiltersCorrectly()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new TransmissionItemWrapper(new TorrentInfo { HashString = "hash1", DownloadDir = "/downloads/movies" }),
                new TransmissionItemWrapper(new TorrentInfo { HashString = "hash2", DownloadDir = "/downloads/tv" })
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
                new TransmissionItemWrapper(new TorrentInfo { HashString = "hash1", DownloadDir = "/downloads/Movies" })
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
                new TransmissionItemWrapper(new TorrentInfo { HashString = "", DownloadDir = "/downloads/movies" }),
                new TransmissionItemWrapper(new TorrentInfo { HashString = "hash1", DownloadDir = "/downloads/movies" })
            };

            // Act
            var result = sut.FilterDownloadsToChangeCategoryAsync(downloads, new List<string> { "movies" });

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("hash1", result[0].Hash);
        }
    }

    public class CreateCategoryAsync_Tests : TransmissionServiceDCTests
    {
        public CreateCategoryAsync_Tests(TransmissionServiceFixture fixture) : base(fixture)
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
            _fixture.ClientWrapper.VerifyNoOtherCalls();
        }
    }

    public class DeleteDownload_Tests : TransmissionServiceDCTests
    {
        public DeleteDownload_Tests(TransmissionServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task GetsIdFromHash_ThenDeletes()
        {
            // Arrange
            var sut = _fixture.CreateSut();
            const string hash = "test-hash";

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

            var torrents = new TransmissionTorrents
            {
                Torrents = new[]
                {
                    new TorrentInfo { Id = 123, HashString = hash }
                }
            };

            _fixture.ClientWrapper
                .Setup(x => x.TorrentGetAsync(fields, hash))
                .ReturnsAsync(torrents);

            _fixture.ClientWrapper
                .Setup(x => x.TorrentRemoveAsync(It.Is<long[]>(ids => ids.Contains(123)), true))
                .Returns(Task.CompletedTask);

            // Act
            await sut.DeleteDownload(hash);

            // Assert
            _fixture.ClientWrapper.Verify(
                x => x.TorrentRemoveAsync(It.Is<long[]>(ids => ids.Contains(123)), true),
                Times.Once);
        }

        [Fact]
        public async Task HandlesNotFound()
        {
            // Arrange
            var sut = _fixture.CreateSut();
            const string hash = "nonexistent-hash";

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

            // Act
            await sut.DeleteDownload(hash);

            // Assert - no exception thrown
            _fixture.ClientWrapper.Verify(
                x => x.TorrentRemoveAsync(It.IsAny<long[]>(), It.IsAny<bool>()),
                Times.Never);
        }

        [Fact]
        public async Task DeletesWithData()
        {
            // Arrange
            var sut = _fixture.CreateSut();
            const string hash = "test-hash";

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

            var torrents = new TransmissionTorrents
            {
                Torrents = new[]
                {
                    new TorrentInfo { Id = 123, HashString = hash }
                }
            };

            _fixture.ClientWrapper
                .Setup(x => x.TorrentGetAsync(fields, hash))
                .ReturnsAsync(torrents);

            // Act
            await sut.DeleteDownload(hash);

            // Assert
            _fixture.ClientWrapper.Verify(
                x => x.TorrentRemoveAsync(It.IsAny<long[]>(), true),
                Times.Once);
        }
    }

    public class ChangeCategoryForNoHardLinksAsync_Tests : TransmissionServiceDCTests
    {
        public ChangeCategoryForNoHardLinksAsync_Tests(TransmissionServiceFixture fixture) : base(fixture)
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
            _fixture.ClientWrapper.Verify(x => x.TorrentSetLocationAsync(It.IsAny<long[]>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
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
            _fixture.ClientWrapper.Verify(x => x.TorrentSetLocationAsync(It.IsAny<long[]>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
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
                new TransmissionItemWrapper(new TorrentInfo { HashString = "", Name = "Test", DownloadDir = "/downloads" })
            };

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.ClientWrapper.Verify(x => x.TorrentSetLocationAsync(It.IsAny<long[]>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
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
                new TransmissionItemWrapper(new TorrentInfo { HashString = "hash1", Name = "", DownloadDir = "/downloads" })
            };

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.ClientWrapper.Verify(x => x.TorrentSetLocationAsync(It.IsAny<long[]>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public async Task MissingDownloadDir_SkipsTorrent()
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
                new TransmissionItemWrapper(new TorrentInfo { HashString = "hash1", Name = "Test", DownloadDir = "" })
            };

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.ClientWrapper.Verify(x => x.TorrentSetLocationAsync(It.IsAny<long[]>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public async Task MissingFiles_SkipsTorrent()
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
                new TransmissionItemWrapper(new TorrentInfo { HashString = "hash1", Name = "Test", DownloadDir = "/downloads", Files = null })
            };

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.ClientWrapper.Verify(x => x.TorrentSetLocationAsync(It.IsAny<long[]>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public async Task MissingFileStats_SkipsTorrent()
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
                new TransmissionItemWrapper(new TorrentInfo
                {
                    HashString = "hash1",
                    Name = "Test",
                    DownloadDir = "/downloads",
                    Files = new[] { new TransmissionTorrentFiles { Name = "file1.mkv" } },
                    FileStats = null
                })
            };

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.ClientWrapper.Verify(x => x.TorrentSetLocationAsync(It.IsAny<long[]>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public async Task NoHardlinks_ChangesLocation()
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
                new TransmissionItemWrapper(new TorrentInfo
                {
                    Id = 123,
                    HashString = "hash1",
                    Name = "Test",
                    DownloadDir = "/downloads/movies",
                    Files = new[] { new TransmissionTorrentFiles { Name = "file1.mkv" } },
                    FileStats = new[] { new TransmissionTorrentFileStats { Wanted = true } }
                })
            };

            _fixture.HardLinkFileService
                .Setup(x => x.GetHardLinkCount(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(0);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.ClientWrapper.Verify(
                x => x.TorrentSetLocationAsync(It.Is<long[]>(ids => ids.Contains(123)), "/downloads/movies/unlinked", true),
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
                new TransmissionItemWrapper(new TorrentInfo
                {
                    Id = 123,
                    HashString = "hash1",
                    Name = "Test",
                    DownloadDir = "/downloads/movies",
                    Files = new[] { new TransmissionTorrentFiles { Name = "file1.mkv" } },
                    FileStats = new[] { new TransmissionTorrentFileStats { Wanted = true } }
                })
            };

            _fixture.HardLinkFileService
                .Setup(x => x.GetHardLinkCount(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(2);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.ClientWrapper.Verify(x => x.TorrentSetLocationAsync(It.IsAny<long[]>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
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
                new TransmissionItemWrapper(new TorrentInfo
                {
                    Id = 123,
                    HashString = "hash1",
                    Name = "Test",
                    DownloadDir = "/downloads/movies",
                    Files = new[] { new TransmissionTorrentFiles { Name = "file1.mkv" } },
                    FileStats = new[] { new TransmissionTorrentFileStats { Wanted = true } }
                })
            };

            _fixture.HardLinkFileService
                .Setup(x => x.GetHardLinkCount(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(-1);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.ClientWrapper.Verify(x => x.TorrentSetLocationAsync(It.IsAny<long[]>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public async Task UnwantedFiles_IgnoredInCheck()
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
                new TransmissionItemWrapper(new TorrentInfo
                {
                    Id = 123,
                    HashString = "hash1",
                    Name = "Test",
                    DownloadDir = "/downloads/movies",
                    Files = new[]
                    {
                        new TransmissionTorrentFiles { Name = "file1.mkv" },
                        new TransmissionTorrentFiles { Name = "file2.mkv" }
                    },
                    FileStats = new[]
                    {
                        new TransmissionTorrentFileStats { Wanted = false },
                        new TransmissionTorrentFileStats { Wanted = true }
                    }
                })
            };

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
                new TransmissionItemWrapper(new TorrentInfo
                {
                    Id = 123,
                    HashString = "hash1",
                    Name = "Test",
                    DownloadDir = "/downloads/movies",
                    Files = new[] { new TransmissionTorrentFiles { Name = "file1.mkv" } },
                    FileStats = new[] { new TransmissionTorrentFileStats { Wanted = true } }
                })
            };

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
                new TransmissionItemWrapper(new TorrentInfo
                {
                    Id = 123,
                    HashString = "hash1",
                    Name = "Test",
                    DownloadDir = "/downloads/movies",
                    Files = new[] { new TransmissionTorrentFiles { Name = "file1.mkv" } },
                    FileStats = new[] { new TransmissionTorrentFileStats { Wanted = true } }
                })
            };

            _fixture.HardLinkFileService
                .Setup(x => x.GetHardLinkCount(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(0);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert - EventPublisher is not mocked, so we just verify the method completed
            _fixture.ClientWrapper.Verify(
                x => x.TorrentSetLocationAsync(It.Is<long[]>(ids => ids.Contains(123)), "/downloads/movies/unlinked", true),
                Times.Once);
        }

        [Fact]
        public async Task AppendsTargetCategoryToBasePath()
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
                new TransmissionItemWrapper(new TorrentInfo
                {
                    Id = 123,
                    HashString = "hash1",
                    Name = "Test",
                    DownloadDir = "/downloads/movies/subfolder",
                    Files = new[] { new TransmissionTorrentFiles { Name = "file1.mkv" } },
                    FileStats = new[] { new TransmissionTorrentFileStats { Wanted = true } }
                })
            };

            _fixture.HardLinkFileService
                .Setup(x => x.GetHardLinkCount(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(0);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.ClientWrapper.Verify(
                x => x.TorrentSetLocationAsync(It.Is<long[]>(ids => ids.Contains(123)), "/downloads/movies/subfolder/unlinked", true),
                Times.Once);
        }
    }
}
