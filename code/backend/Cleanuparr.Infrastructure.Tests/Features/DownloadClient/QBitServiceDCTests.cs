using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient.QBittorrent;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Moq;
using QBittorrent.Client;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient;

public class QBitServiceDCTests : IClassFixture<QBitServiceFixture>
{
    private readonly QBitServiceFixture _fixture;

    public QBitServiceDCTests(QBitServiceFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetMocks();
    }

    public class GetSeedingDownloads_Tests : QBitServiceDCTests
    {
        public GetSeedingDownloads_Tests(QBitServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task ReturnsCompletedTorrents()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var torrentList = new[]
            {
                new TorrentInfo { Hash = "hash1", Name = "Torrent 1", State = TorrentState.Uploading },
                new TorrentInfo { Hash = "hash2", Name = "Torrent 2", State = TorrentState.Uploading }
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentListAsync(It.Is<TorrentListQuery>(q => q.Filter == TorrentListFilter.Completed)))
                .ReturnsAsync(torrentList);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentTrackersAsync("hash1"))
                .ReturnsAsync(Array.Empty<TorrentTracker>());

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentTrackersAsync("hash2"))
                .ReturnsAsync(Array.Empty<TorrentTracker>());

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentPropertiesAsync(It.IsAny<string>()))
                .ReturnsAsync(new TorrentProperties
                {
                    AdditionalData = new Dictionary<string, Newtonsoft.Json.Linq.JToken>
                    {
                        { "is_private", Newtonsoft.Json.Linq.JToken.FromObject(false) }
                    }
                });

            // Act
            var result = await sut.GetSeedingDownloads();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.All(result, item => Assert.NotNull(item.Hash));
        }

        [Fact]
        public async Task SetsIsPrivateCorrectly_WhenPrivate()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var torrentList = new[]
            {
                new TorrentInfo { Hash = "hash1", Name = "Private Torrent", State = TorrentState.Uploading }
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentListAsync(It.Is<TorrentListQuery>(q => q.Filter == TorrentListFilter.Completed)))
                .ReturnsAsync(torrentList);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentTrackersAsync("hash1"))
                .ReturnsAsync(Array.Empty<TorrentTracker>());

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentPropertiesAsync("hash1"))
                .ReturnsAsync(new TorrentProperties
                {
                    AdditionalData = new Dictionary<string, Newtonsoft.Json.Linq.JToken>
                    {
                        { "is_private", Newtonsoft.Json.Linq.JToken.FromObject(true) }
                    }
                });

            // Act
            var result = await sut.GetSeedingDownloads();

            // Assert
            Assert.Single(result);
            Assert.True(result[0].IsPrivate);
        }

        [Fact]
        public async Task SetsIsPrivateCorrectly_WhenPublic()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var torrentList = new[]
            {
                new TorrentInfo { Hash = "hash1", Name = "Public Torrent", State = TorrentState.Uploading }
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentListAsync(It.Is<TorrentListQuery>(q => q.Filter == TorrentListFilter.Completed)))
                .ReturnsAsync(torrentList);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentTrackersAsync("hash1"))
                .ReturnsAsync(Array.Empty<TorrentTracker>());

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentPropertiesAsync("hash1"))
                .ReturnsAsync(new TorrentProperties
                {
                    AdditionalData = new Dictionary<string, Newtonsoft.Json.Linq.JToken>
                    {
                        { "is_private", Newtonsoft.Json.Linq.JToken.FromObject(false) }
                    }
                });

            // Act
            var result = await sut.GetSeedingDownloads();

            // Assert
            Assert.Single(result);
            Assert.False(result[0].IsPrivate);
        }

        [Fact]
        public async Task ReturnsEmptyList_WhenNoTorrents()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentListAsync(It.Is<TorrentListQuery>(q => q.Filter == TorrentListFilter.Completed)))
                .ReturnsAsync((TorrentInfo[]?)null);

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

            var torrentList = new[]
            {
                new TorrentInfo { Hash = "", Name = "No Hash", State = TorrentState.Uploading },
                new TorrentInfo { Hash = "hash1", Name = "Valid Hash", State = TorrentState.Uploading }
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentListAsync(It.Is<TorrentListQuery>(q => q.Filter == TorrentListFilter.Completed)))
                .ReturnsAsync(torrentList);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentTrackersAsync("hash1"))
                .ReturnsAsync(Array.Empty<TorrentTracker>());

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentPropertiesAsync("hash1"))
                .ReturnsAsync(new TorrentProperties
                {
                    AdditionalData = new Dictionary<string, Newtonsoft.Json.Linq.JToken>
                    {
                        { "is_private", Newtonsoft.Json.Linq.JToken.FromObject(false) }
                    }
                });

            // Act
            var result = await sut.GetSeedingDownloads();

            // Assert
            Assert.Single(result);
            Assert.Equal("hash1", result[0].Hash);
        }
    }

    public class FilterDownloadsToBeCleanedAsync_Tests : QBitServiceDCTests
    {
        public FilterDownloadsToBeCleanedAsync_Tests(QBitServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public void MatchesCategories()
        {
            // Arrange
            var sut = _fixture.CreateSut();
            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new QBitItemWrapper(new TorrentInfo { Hash = "hash1", Category = "movies" }, Array.Empty<TorrentTracker>(), false),
                new QBitItemWrapper(new TorrentInfo { Hash = "hash2", Category = "tv" }, Array.Empty<TorrentTracker>(), false),
                new QBitItemWrapper(new TorrentInfo { Hash = "hash3", Category = "music" }, Array.Empty<TorrentTracker>(), false)
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
                new QBitItemWrapper(new TorrentInfo { Hash = "hash1", Category = "Movies" }, Array.Empty<TorrentTracker>(), false)
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
        public void SkipsDownloadsWithEmptyHash()
        {
            // Arrange
            var sut = _fixture.CreateSut();
            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new QBitItemWrapper(new TorrentInfo { Hash = "", Category = "movies" }, Array.Empty<TorrentTracker>(), false),
                new QBitItemWrapper(new TorrentInfo { Hash = "hash1", Category = "movies" }, Array.Empty<TorrentTracker>(), false)
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
            Assert.Equal("hash1", result[0].Hash);
        }

        [Fact]
        public void ReturnsEmptyList_WhenNoMatches()
        {
            // Arrange
            var sut = _fixture.CreateSut();
            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new QBitItemWrapper(new TorrentInfo { Hash = "hash1", Category = "music" }, Array.Empty<TorrentTracker>(), false)
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

    public class FilterDownloadsToChangeCategoryAsync_Tests : QBitServiceDCTests
    {
        public FilterDownloadsToChangeCategoryAsync_Tests(QBitServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public void ExcludesAlreadyTagged_WhenTagModeEnabled()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var config = new DownloadCleanerConfig
            {
                Id = Guid.NewGuid(),
                UnlinkedUseTag = true,
                UnlinkedTargetCategory = "unlinked"
            };
            ContextProvider.Set(nameof(DownloadCleanerConfig), config);

            var torrentInfo1 = new TorrentInfo { Hash = "hash1", Category = "movies", Tags = new[] { "unlinked" } };
            var torrentInfo2 = new TorrentInfo { Hash = "hash2", Category = "movies", Tags = Array.Empty<string>() };

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new QBitItemWrapper(torrentInfo1, Array.Empty<TorrentTracker>(), false),
                new QBitItemWrapper(torrentInfo2, Array.Empty<TorrentTracker>(), false)
            };

            // Act
            var result = sut.FilterDownloadsToChangeCategoryAsync(downloads, new List<string> { "movies" });

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("hash2", result[0].Hash);
        }

        [Fact]
        public void IncludesAll_WhenCategoryModeEnabled()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var config = new DownloadCleanerConfig
            {
                Id = Guid.NewGuid(),
                UnlinkedUseTag = false,
                UnlinkedTargetCategory = "unlinked"
            };
            ContextProvider.Set(nameof(DownloadCleanerConfig), config);

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new QBitItemWrapper(new TorrentInfo { Hash = "hash1", Category = "movies" }, Array.Empty<TorrentTracker>(), false),
                new QBitItemWrapper(new TorrentInfo { Hash = "hash2", Category = "movies" }, Array.Empty<TorrentTracker>(), false)
            };

            // Act
            var result = sut.FilterDownloadsToChangeCategoryAsync(downloads, new List<string> { "movies" });

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void IsCaseInsensitive()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var config = new DownloadCleanerConfig
            {
                Id = Guid.NewGuid(),
                UnlinkedUseTag = false
            };
            ContextProvider.Set(nameof(DownloadCleanerConfig), config);

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new QBitItemWrapper(new TorrentInfo { Hash = "hash1", Category = "Movies" }, Array.Empty<TorrentTracker>(), false)
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

            var config = new DownloadCleanerConfig
            {
                Id = Guid.NewGuid(),
                UnlinkedUseTag = false
            };
            ContextProvider.Set(nameof(DownloadCleanerConfig), config);

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new QBitItemWrapper(new TorrentInfo { Hash = "", Category = "movies" }, Array.Empty<TorrentTracker>(), false),
                new QBitItemWrapper(new TorrentInfo { Hash = "hash1", Category = "movies" }, Array.Empty<TorrentTracker>(), false)
            };

            // Act
            var result = sut.FilterDownloadsToChangeCategoryAsync(downloads, new List<string> { "movies" });

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("hash1", result[0].Hash);
        }
    }

    public class CreateCategoryAsync_Tests : QBitServiceDCTests
    {
        public CreateCategoryAsync_Tests(QBitServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task CreatesCategory_WhenMissing()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            _fixture.ClientWrapper
                .Setup(x => x.GetCategoriesAsync())
                .ReturnsAsync(new Dictionary<string, Category>());

            _fixture.ClientWrapper
                .Setup(x => x.AddCategoryAsync("new-category"))
                .Returns(Task.CompletedTask);

            // Act
            await sut.CreateCategoryAsync("new-category");

            // Assert
            _fixture.ClientWrapper.Verify(x => x.AddCategoryAsync("new-category"), Times.Once);
        }

        [Fact]
        public async Task SkipsCreation_WhenCategoryExists()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            _fixture.ClientWrapper
                .Setup(x => x.GetCategoriesAsync())
                .ReturnsAsync(new Dictionary<string, Category>
                {
                    { "existing", new Category { Name = "existing" } }
                });

            // Act
            await sut.CreateCategoryAsync("existing");

            // Assert
            _fixture.ClientWrapper.Verify(x => x.AddCategoryAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task IsCaseInsensitive()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            _fixture.ClientWrapper
                .Setup(x => x.GetCategoriesAsync())
                .ReturnsAsync(new Dictionary<string, Category>
                {
                    { "existing", new Category { Name = "Existing" } }
                });

            // Act
            await sut.CreateCategoryAsync("existing");

            // Assert
            _fixture.ClientWrapper.Verify(x => x.AddCategoryAsync(It.IsAny<string>()), Times.Never);
        }
    }

    public class DeleteDownload_Tests : QBitServiceDCTests
    {
        public DeleteDownload_Tests(QBitServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task CallsClientDelete()
        {
            // Arrange
            var sut = _fixture.CreateSut();
            const string hash = "test-hash";
            var mockTorrent = new Mock<ITorrentItemWrapper>();
            mockTorrent.Setup(x => x.Hash).Returns(hash);

            _fixture.ClientWrapper
                .Setup(x => x.DeleteAsync(It.Is<IEnumerable<string>>(h => h.Contains(hash)), true))
                .Returns(Task.CompletedTask);

            // Act
            await sut.DeleteDownload(mockTorrent.Object, true);

            // Assert
            _fixture.ClientWrapper.Verify(
                x => x.DeleteAsync(It.Is<IEnumerable<string>>(h => h.Contains(hash)), true),
                Times.Once);
        }

        [Fact]
        public async Task DeletesWithData()
        {
            // Arrange
            var sut = _fixture.CreateSut();
            const string hash = "test-hash";
            var mockTorrent = new Mock<ITorrentItemWrapper>();
            mockTorrent.Setup(x => x.Hash).Returns(hash);

            _fixture.ClientWrapper
                .Setup(x => x.DeleteAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<bool>()))
                .Returns(Task.CompletedTask);

            // Act
            await sut.DeleteDownload(mockTorrent.Object, true);

            // Assert
            _fixture.ClientWrapper.Verify(
                x => x.DeleteAsync(It.IsAny<IEnumerable<string>>(), true),
                Times.Once);
        }
    }

    public class ChangeCategoryForNoHardLinksAsync_Tests : QBitServiceDCTests
    {
        public ChangeCategoryForNoHardLinksAsync_Tests(QBitServiceFixture fixture) : base(fixture)
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
                UnlinkedUseTag = false,
                UnlinkedTargetCategory = "unlinked"
            };
            ContextProvider.Set(nameof(DownloadCleanerConfig), config);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(null);

            // Assert - no exceptions thrown
            _fixture.ClientWrapper.Verify(x => x.SetTorrentCategoryAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task EmptyDownloads_DoesNothing()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var config = new DownloadCleanerConfig
            {
                Id = Guid.NewGuid(),
                UnlinkedUseTag = false,
                UnlinkedTargetCategory = "unlinked"
            };
            ContextProvider.Set(nameof(DownloadCleanerConfig), config);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(new List<Domain.Entities.ITorrentItemWrapper>());

            // Assert
            _fixture.ClientWrapper.Verify(x => x.SetTorrentCategoryAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task MissingHash_SkipsTorrent()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var config = new DownloadCleanerConfig
            {
                Id = Guid.NewGuid(),
                UnlinkedUseTag = false,
                UnlinkedTargetCategory = "unlinked"
            };
            ContextProvider.Set(nameof(DownloadCleanerConfig), config);

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new QBitItemWrapper(new TorrentInfo { Hash = "", Name = "Test", Category = "movies", SavePath = "/downloads" }, Array.Empty<TorrentTracker>(), false)
            };

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.ClientWrapper.Verify(x => x.SetTorrentCategoryAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task MissingName_SkipsTorrent()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var config = new DownloadCleanerConfig
            {
                Id = Guid.NewGuid(),
                UnlinkedUseTag = false,
                UnlinkedTargetCategory = "unlinked"
            };
            ContextProvider.Set(nameof(DownloadCleanerConfig), config);

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new QBitItemWrapper(new TorrentInfo { Hash = "hash1", Name = "", Category = "movies", SavePath = "/downloads" }, Array.Empty<TorrentTracker>(), false)
            };

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.ClientWrapper.Verify(x => x.SetTorrentCategoryAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task MissingCategory_SkipsTorrent()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var config = new DownloadCleanerConfig
            {
                Id = Guid.NewGuid(),
                UnlinkedUseTag = false,
                UnlinkedTargetCategory = "unlinked"
            };
            ContextProvider.Set(nameof(DownloadCleanerConfig), config);

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new QBitItemWrapper(new TorrentInfo { Hash = "hash1", Name = "Test", Category = "", SavePath = "/downloads" }, Array.Empty<TorrentTracker>(), false)
            };

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.ClientWrapper.Verify(x => x.SetTorrentCategoryAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task NoFiles_SkipsTorrent()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var config = new DownloadCleanerConfig
            {
                Id = Guid.NewGuid(),
                UnlinkedUseTag = false,
                UnlinkedTargetCategory = "unlinked"
            };
            ContextProvider.Set(nameof(DownloadCleanerConfig), config);

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new QBitItemWrapper(new TorrentInfo { Hash = "hash1", Name = "Test", Category = "movies", SavePath = "/downloads" }, Array.Empty<TorrentTracker>(), false)
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentContentsAsync("hash1"))
                .ReturnsAsync((IReadOnlyList<TorrentContent>?)null);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.ClientWrapper.Verify(x => x.SetTorrentCategoryAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task NoHardlinks_ChangesCategory()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var config = new DownloadCleanerConfig
            {
                Id = Guid.NewGuid(),
                UnlinkedUseTag = false,
                UnlinkedTargetCategory = "unlinked"
            };
            ContextProvider.Set(nameof(DownloadCleanerConfig), config);

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new QBitItemWrapper(new TorrentInfo { Hash = "hash1", Name = "Test", Category = "movies", SavePath = "/downloads" }, Array.Empty<TorrentTracker>(), false)
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentContentsAsync("hash1"))
                .ReturnsAsync(new[]
                {
                    new TorrentContent { Index = 0, Name = "file1.mkv", Priority = TorrentContentPriority.Normal }
                });

            _fixture.HardLinkFileService
                .Setup(x => x.GetHardLinkCount(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(0);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.ClientWrapper.Verify(
                x => x.SetTorrentCategoryAsync(It.Is<IEnumerable<string>>(h => h.Contains("hash1")), "unlinked"),
                Times.Once);
        }

        [Fact]
        public async Task NoHardlinks_TagMode_AddsTag()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var config = new DownloadCleanerConfig
            {
                Id = Guid.NewGuid(),
                UnlinkedUseTag = true,
                UnlinkedTargetCategory = "unlinked"
            };
            ContextProvider.Set(nameof(DownloadCleanerConfig), config);

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new QBitItemWrapper(new TorrentInfo { Hash = "hash1", Name = "Test", Category = "movies", SavePath = "/downloads" }, Array.Empty<TorrentTracker>(), false)
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentContentsAsync("hash1"))
                .ReturnsAsync(new[]
                {
                    new TorrentContent { Index = 0, Name = "file1.mkv", Priority = TorrentContentPriority.Normal }
                });

            _fixture.HardLinkFileService
                .Setup(x => x.GetHardLinkCount(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(0);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.ClientWrapper.Verify(
                x => x.AddTorrentTagAsync(It.Is<IEnumerable<string>>(h => h.Contains("hash1")), "unlinked"),
                Times.Once);
            _fixture.ClientWrapper.Verify(
                x => x.SetTorrentCategoryAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task HasHardlinks_SkipsTorrent()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var config = new DownloadCleanerConfig
            {
                Id = Guid.NewGuid(),
                UnlinkedUseTag = false,
                UnlinkedTargetCategory = "unlinked"
            };
            ContextProvider.Set(nameof(DownloadCleanerConfig), config);

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new QBitItemWrapper(new TorrentInfo { Hash = "hash1", Name = "Test", Category = "movies", SavePath = "/downloads" }, Array.Empty<TorrentTracker>(), false)
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentContentsAsync("hash1"))
                .ReturnsAsync(new[]
                {
                    new TorrentContent { Index = 0, Name = "file1.mkv", Priority = TorrentContentPriority.Normal }
                });

            _fixture.HardLinkFileService
                .Setup(x => x.GetHardLinkCount(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(2); // Has hardlinks

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.ClientWrapper.Verify(x => x.SetTorrentCategoryAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task FileNotFound_SkipsTorrent()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var config = new DownloadCleanerConfig
            {
                Id = Guid.NewGuid(),
                UnlinkedUseTag = false,
                UnlinkedTargetCategory = "unlinked"
            };
            ContextProvider.Set(nameof(DownloadCleanerConfig), config);

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new QBitItemWrapper(new TorrentInfo { Hash = "hash1", Name = "Test", Category = "movies", SavePath = "/downloads" }, Array.Empty<TorrentTracker>(), false)
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentContentsAsync("hash1"))
                .ReturnsAsync(new[]
                {
                    new TorrentContent { Index = 0, Name = "file1.mkv", Priority = TorrentContentPriority.Normal }
                });

            _fixture.HardLinkFileService
                .Setup(x => x.GetHardLinkCount(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(-1); // Error

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.ClientWrapper.Verify(x => x.SetTorrentCategoryAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task SkippedFiles_IgnoredInCheck()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var config = new DownloadCleanerConfig
            {
                Id = Guid.NewGuid(),
                UnlinkedUseTag = false,
                UnlinkedTargetCategory = "unlinked"
            };
            ContextProvider.Set(nameof(DownloadCleanerConfig), config);

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new QBitItemWrapper(new TorrentInfo { Hash = "hash1", Name = "Test", Category = "movies", SavePath = "/downloads" }, Array.Empty<TorrentTracker>(), false)
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentContentsAsync("hash1"))
                .ReturnsAsync(new[]
                {
                    new TorrentContent { Index = 0, Name = "file1.mkv", Priority = TorrentContentPriority.Skip },
                    new TorrentContent { Index = 1, Name = "file2.mkv", Priority = TorrentContentPriority.Normal }
                });

            _fixture.HardLinkFileService
                .Setup(x => x.GetHardLinkCount(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(0);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.HardLinkFileService.Verify(
                x => x.GetHardLinkCount(It.IsAny<string>(), It.IsAny<bool>()),
                Times.Once); // Only called for file2.mkv
        }

        [Fact]
        public async Task FileWithNullIndex_SkipsTorrent()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var config = new DownloadCleanerConfig
            {
                Id = Guid.NewGuid(),
                UnlinkedUseTag = false,
                UnlinkedTargetCategory = "unlinked"
            };
            ContextProvider.Set(nameof(DownloadCleanerConfig), config);

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new QBitItemWrapper(new TorrentInfo { Hash = "hash1", Name = "Test", Category = "movies", SavePath = "/downloads" }, Array.Empty<TorrentTracker>(), false)
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentContentsAsync("hash1"))
                .ReturnsAsync(new[]
                {
                    new TorrentContent { Index = null, Name = "file1.mkv", Priority = TorrentContentPriority.Normal }
                });

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert
            _fixture.ClientWrapper.Verify(x => x.SetTorrentCategoryAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task PublishesCategoryChangedEvent()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var config = new DownloadCleanerConfig
            {
                Id = Guid.NewGuid(),
                UnlinkedUseTag = false,
                UnlinkedTargetCategory = "unlinked"
            };
            ContextProvider.Set(nameof(DownloadCleanerConfig), config);

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new QBitItemWrapper(new TorrentInfo { Hash = "hash1", Name = "Test", Category = "movies", SavePath = "/downloads" }, Array.Empty<TorrentTracker>(), false)
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentContentsAsync("hash1"))
                .ReturnsAsync(new[]
                {
                    new TorrentContent { Index = 0, Name = "file1.mkv", Priority = TorrentContentPriority.Normal }
                });

            _fixture.HardLinkFileService
                .Setup(x => x.GetHardLinkCount(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(0);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert - EventPublisher is not mocked, so we just verify the method completed
            _fixture.ClientWrapper.Verify(
                x => x.SetTorrentCategoryAsync(It.Is<IEnumerable<string>>(h => h.Contains("hash1")), "unlinked"),
                Times.Once);
        }

        [Fact]
        public async Task PublishesCategoryChangedEvent_WithTagFlag()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var config = new DownloadCleanerConfig
            {
                Id = Guid.NewGuid(),
                UnlinkedUseTag = true,
                UnlinkedTargetCategory = "unlinked"
            };
            ContextProvider.Set(nameof(DownloadCleanerConfig), config);

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new QBitItemWrapper(new TorrentInfo { Hash = "hash1", Name = "Test", Category = "movies", SavePath = "/downloads" }, Array.Empty<TorrentTracker>(), false)
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentContentsAsync("hash1"))
                .ReturnsAsync(new[]
                {
                    new TorrentContent { Index = 0, Name = "file1.mkv", Priority = TorrentContentPriority.Normal }
                });

            _fixture.HardLinkFileService
                .Setup(x => x.GetHardLinkCount(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(0);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads);

            // Assert - EventPublisher is not mocked, so we just verify the method completed
            _fixture.ClientWrapper.Verify(
                x => x.AddTorrentTagAsync(It.Is<IEnumerable<string>>(h => h.Contains("hash1")), "unlinked"),
                Times.Once);
        }
    }
}
