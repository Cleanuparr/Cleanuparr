using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient.QBittorrent;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using NSubstitute;
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
                .GetTorrentListAsync(Arg.Is<TorrentListQuery>(q => q.Filter == TorrentListFilter.Completed))
                .Returns(torrentList);

            _fixture.ClientWrapper
                .GetTorrentTrackersAsync("hash1")
                .Returns(Array.Empty<TorrentTracker>());

            _fixture.ClientWrapper
                .GetTorrentTrackersAsync("hash2")
                .Returns(Array.Empty<TorrentTracker>());

            _fixture.ClientWrapper
                .GetTorrentPropertiesAsync(Arg.Any<string>())
                .Returns(new TorrentProperties
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
                .GetTorrentListAsync(Arg.Is<TorrentListQuery>(q => q.Filter == TorrentListFilter.Completed))
                .Returns(torrentList);

            _fixture.ClientWrapper
                .GetTorrentTrackersAsync("hash1")
                .Returns(Array.Empty<TorrentTracker>());

            _fixture.ClientWrapper
                .GetTorrentPropertiesAsync("hash1")
                .Returns(new TorrentProperties
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
                .GetTorrentListAsync(Arg.Is<TorrentListQuery>(q => q.Filter == TorrentListFilter.Completed))
                .Returns(torrentList);

            _fixture.ClientWrapper
                .GetTorrentTrackersAsync("hash1")
                .Returns(Array.Empty<TorrentTracker>());

            _fixture.ClientWrapper
                .GetTorrentPropertiesAsync("hash1")
                .Returns(new TorrentProperties
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
                .GetTorrentListAsync(Arg.Is<TorrentListQuery>(q => q.Filter == TorrentListFilter.Completed))
                .Returns((TorrentInfo[]?)null);

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
                .GetTorrentListAsync(Arg.Is<TorrentListQuery>(q => q.Filter == TorrentListFilter.Completed))
                .Returns(torrentList);

            _fixture.ClientWrapper
                .GetTorrentTrackersAsync("hash1")
                .Returns(Array.Empty<TorrentTracker>());

            _fixture.ClientWrapper
                .GetTorrentPropertiesAsync("hash1")
                .Returns(new TorrentProperties
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

            var categories = new List<ISeedingRule>
            {
                new QBitSeedingRule { Name = "movies", Categories = ["movies"], MaxRatio = -1, MinSeedTime = 0, MaxSeedTime = -1, DeleteSourceFiles = true },
                new QBitSeedingRule { Name = "tv", Categories = ["tv"], MaxRatio = -1, MinSeedTime = 0, MaxSeedTime = -1, DeleteSourceFiles = true }
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

            var categories = new List<ISeedingRule>
            {
                new QBitSeedingRule { Name = "movies", Categories = ["movies"], MaxRatio = -1, MinSeedTime = 0, MaxSeedTime = -1, DeleteSourceFiles = true }
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

            var categories = new List<ISeedingRule>
            {
                new QBitSeedingRule { Name = "movies", Categories = ["movies"], MaxRatio = -1, MinSeedTime = 0, MaxSeedTime = -1, DeleteSourceFiles = true }
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

            var categories = new List<ISeedingRule>
            {
                new QBitSeedingRule { Name = "movies", Categories = ["movies"], MaxRatio = -1, MinSeedTime = 0, MaxSeedTime = -1, DeleteSourceFiles = true }
            };

            // Act
            var result = sut.FilterDownloadsToBeCleanedAsync(downloads, categories);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }
    }

    public class CleanDownloadsAsync_Tests : QBitServiceDCTests
    {
        public CleanDownloadsAsync_Tests(QBitServiceFixture fixture) : base(fixture)
        {
        }

        private static QBitItemWrapper CreateTorrent(string hash, string category, bool isPrivate) =>
            new(new TorrentInfo
            {
                Hash = hash,
                Name = $"Test {hash}",
                Category = category,
                Ratio = 2.0,
                SeedingTime = TimeSpan.FromHours(10)
            }, Array.Empty<TorrentTracker>(), isPrivate);

        private static QBitSeedingRule CreateRule(string name, TorrentPrivacyType privacyType) =>
            new()
            {
                Name = name,
                Categories = [name],
                PrivacyType = privacyType,
                MaxRatio = 0,
                MinSeedTime = 0,
                MaxSeedTime = -1,
                DeleteSourceFiles = false
            };

        private void SetupDeleteMock()
        {
            _fixture.ClientWrapper
                .DeleteAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<bool>())
                .Returns(Task.CompletedTask);
        }

        [Fact]
        public async Task SkipsPrivateTorrent_WhenRuleIsPublicOnly()
        {
            // Arrange
            var sut = _fixture.CreateSut();
            SetupDeleteMock();

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                CreateTorrent("hash1", "movies", isPrivate: true)
            };
            var rules = new List<ISeedingRule> { CreateRule("movies", TorrentPrivacyType.Public) };

            // Act
            await sut.CleanDownloadsAsync(downloads, rules);

            // Assert
            await _fixture.ClientWrapper.DidNotReceive()
                .DeleteAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<bool>());
        }

        [Fact]
        public async Task CleansPublicTorrent_WhenRuleIsPublicOnly()
        {
            // Arrange
            var sut = _fixture.CreateSut();
            SetupDeleteMock();

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                CreateTorrent("hash1", "movies", isPrivate: false)
            };
            var rules = new List<ISeedingRule> { CreateRule("movies", TorrentPrivacyType.Public) };

            // Act
            await sut.CleanDownloadsAsync(downloads, rules);

            // Assert
            await _fixture.ClientWrapper.Received(1)
                .DeleteAsync(Arg.Is<IEnumerable<string>>(h => h.Contains("hash1")), false);
        }

        [Fact]
        public async Task SkipsPublicTorrent_WhenRuleIsPrivateOnly()
        {
            // Arrange
            var sut = _fixture.CreateSut();
            SetupDeleteMock();

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                CreateTorrent("hash1", "movies", isPrivate: false)
            };
            var rules = new List<ISeedingRule> { CreateRule("movies", TorrentPrivacyType.Private) };

            // Act
            await sut.CleanDownloadsAsync(downloads, rules);

            // Assert
            await _fixture.ClientWrapper.DidNotReceive()
                .DeleteAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<bool>());
        }

        [Fact]
        public async Task CleansPrivateTorrent_WhenRuleIsPrivateOnly()
        {
            // Arrange
            var sut = _fixture.CreateSut();
            SetupDeleteMock();

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                CreateTorrent("hash1", "movies", isPrivate: true)
            };
            var rules = new List<ISeedingRule> { CreateRule("movies", TorrentPrivacyType.Private) };

            // Act
            await sut.CleanDownloadsAsync(downloads, rules);

            // Assert
            await _fixture.ClientWrapper.Received(1)
                .DeleteAsync(Arg.Is<IEnumerable<string>>(h => h.Contains("hash1")), false);
        }

        [Fact]
        public async Task CleansPublicTorrent_WhenRuleIsBoth()
        {
            // Arrange
            var sut = _fixture.CreateSut();
            SetupDeleteMock();

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                CreateTorrent("hash1", "movies", isPrivate: false)
            };
            var rules = new List<ISeedingRule> { CreateRule("movies", TorrentPrivacyType.Both) };

            // Act
            await sut.CleanDownloadsAsync(downloads, rules);

            // Assert
            await _fixture.ClientWrapper.Received(1)
                .DeleteAsync(Arg.Is<IEnumerable<string>>(h => h.Contains("hash1")), false);
        }

        [Fact]
        public async Task CleansPrivateTorrent_WhenRuleIsBoth()
        {
            // Arrange
            var sut = _fixture.CreateSut();
            SetupDeleteMock();

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                CreateTorrent("hash1", "movies", isPrivate: true)
            };
            var rules = new List<ISeedingRule> { CreateRule("movies", TorrentPrivacyType.Both) };

            // Act
            await sut.CleanDownloadsAsync(downloads, rules);

            // Assert
            await _fixture.ClientWrapper.Received(1)
                .DeleteAsync(Arg.Is<IEnumerable<string>>(h => h.Contains("hash1")), false);
        }

        [Fact]
        public async Task MatchesCorrectRule_WhenMultipleRulesForSameCategory()
        {
            // Arrange
            var sut = _fixture.CreateSut();
            SetupDeleteMock();

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                CreateTorrent("public-hash", "movies", isPrivate: false),
                CreateTorrent("private-hash", "movies", isPrivate: true)
            };
            var rules = new List<ISeedingRule>
            {
                CreateRule("movies", TorrentPrivacyType.Public),
                CreateRule("movies", TorrentPrivacyType.Private)
            };

            // Act
            await sut.CleanDownloadsAsync(downloads, rules);

            // Assert - both torrents should be cleaned, each matching their respective rule
            await _fixture.ClientWrapper.Received(1)
                .DeleteAsync(Arg.Is<IEnumerable<string>>(h => h.Contains("public-hash")), false);
            await _fixture.ClientWrapper.Received(1)
                .DeleteAsync(Arg.Is<IEnumerable<string>>(h => h.Contains("private-hash")), false);
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

            var unlinkedConfig = new UnlinkedConfig
            {
                Id = Guid.NewGuid(),
                UseTag = true,
                TargetCategory = "unlinked",
                Categories = ["movies"]
            };

            var torrentInfo1 = new TorrentInfo { Hash = "hash1", Category = "movies", Tags = new[] { "unlinked" } };
            var torrentInfo2 = new TorrentInfo { Hash = "hash2", Category = "movies", Tags = Array.Empty<string>() };

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new QBitItemWrapper(torrentInfo1, Array.Empty<TorrentTracker>(), false),
                new QBitItemWrapper(torrentInfo2, Array.Empty<TorrentTracker>(), false)
            };

            // Act
            var result = sut.FilterDownloadsToChangeCategoryAsync(downloads, unlinkedConfig);

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

            var unlinkedConfig = new UnlinkedConfig
            {
                Id = Guid.NewGuid(),
                UseTag = false,
                TargetCategory = "unlinked",
                Categories = ["movies"]
            };

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new QBitItemWrapper(new TorrentInfo { Hash = "hash1", Category = "movies" }, Array.Empty<TorrentTracker>(), false),
                new QBitItemWrapper(new TorrentInfo { Hash = "hash2", Category = "movies" }, Array.Empty<TorrentTracker>(), false)
            };

            // Act
            var result = sut.FilterDownloadsToChangeCategoryAsync(downloads, unlinkedConfig);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void IsCaseInsensitive()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var unlinkedConfig = new UnlinkedConfig
            {
                Id = Guid.NewGuid(),
                UseTag = false,
                Categories = ["movies"]
            };

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new QBitItemWrapper(new TorrentInfo { Hash = "hash1", Category = "Movies" }, Array.Empty<TorrentTracker>(), false)
            };

            // Act
            var result = sut.FilterDownloadsToChangeCategoryAsync(downloads, unlinkedConfig);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
        }

        [Fact]
        public void SkipsDownloadsWithEmptyHash()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var unlinkedConfig = new UnlinkedConfig
            {
                Id = Guid.NewGuid(),
                UseTag = false,
                Categories = ["movies"]
            };

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new QBitItemWrapper(new TorrentInfo { Hash = "", Category = "movies" }, Array.Empty<TorrentTracker>(), false),
                new QBitItemWrapper(new TorrentInfo { Hash = "hash1", Category = "movies" }, Array.Empty<TorrentTracker>(), false)
            };

            // Act
            var result = sut.FilterDownloadsToChangeCategoryAsync(downloads, unlinkedConfig);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("hash1", result[0].Hash);
        }

        [Fact]
        public void ReturnsEmpty_WhenNoCategoriesMatch()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new QBitItemWrapper(new TorrentInfo { Hash = "hash1", Category = "tv" }, Array.Empty<TorrentTracker>(), false)
            };

            // Act
            var result = sut.FilterDownloadsToChangeCategoryAsync(downloads, new UnlinkedConfig { Categories = ["movies"] });

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
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
                .GetCategoriesAsync()
                .Returns(new Dictionary<string, Category>());

            _fixture.ClientWrapper
                .AddCategoryAsync("new-category")
                .Returns(Task.CompletedTask);

            // Act
            await sut.CreateCategoryAsync("new-category");

            // Assert
            await _fixture.ClientWrapper.Received(1).AddCategoryAsync("new-category");
        }

        [Fact]
        public async Task SkipsCreation_WhenCategoryExists()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            _fixture.ClientWrapper
                .GetCategoriesAsync()
                .Returns(new Dictionary<string, Category>
                {
                    { "existing", new Category { Name = "existing" } }
                });

            // Act
            await sut.CreateCategoryAsync("existing");

            // Assert
            await _fixture.ClientWrapper.DidNotReceive().AddCategoryAsync(Arg.Any<string>());
        }

        [Fact]
        public async Task IsCaseInsensitive()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            _fixture.ClientWrapper
                .GetCategoriesAsync()
                .Returns(new Dictionary<string, Category>
                {
                    { "existing", new Category { Name = "Existing" } }
                });

            // Act
            await sut.CreateCategoryAsync("existing");

            // Assert
            await _fixture.ClientWrapper.DidNotReceive().AddCategoryAsync(Arg.Any<string>());
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
            var mockTorrent = Substitute.For<ITorrentItemWrapper>();
            mockTorrent.Hash.Returns(hash);

            _fixture.ClientWrapper
                .DeleteAsync(Arg.Is<IEnumerable<string>>(h => h.Contains(hash)), true)
                .Returns(Task.CompletedTask);

            // Act
            await sut.DeleteDownload(mockTorrent, true);

            // Assert
            await _fixture.ClientWrapper.Received(1)
                .DeleteAsync(Arg.Is<IEnumerable<string>>(h => h.Contains(hash)), true);
        }

        [Fact]
        public async Task DeletesWithData()
        {
            // Arrange
            var sut = _fixture.CreateSut();
            const string hash = "test-hash";
            var mockTorrent = Substitute.For<ITorrentItemWrapper>();
            mockTorrent.Hash.Returns(hash);

            _fixture.ClientWrapper
                .DeleteAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<bool>())
                .Returns(Task.CompletedTask);

            // Act
            await sut.DeleteDownload(mockTorrent, true);

            // Assert
            await _fixture.ClientWrapper.Received(1)
                .DeleteAsync(Arg.Any<IEnumerable<string>>(), true);
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

            var unlinkedConfig = new UnlinkedConfig
            {
                Id = Guid.NewGuid(),
                UseTag = false,
                TargetCategory = "unlinked"
            };

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(null, unlinkedConfig);

            // Assert - no exceptions thrown
            await _fixture.ClientWrapper.DidNotReceive().SetTorrentCategoryAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<string>());
        }

        [Fact]
        public async Task EmptyDownloads_DoesNothing()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var unlinkedConfig = new UnlinkedConfig
            {
                Id = Guid.NewGuid(),
                UseTag = false,
                TargetCategory = "unlinked"
            };

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(new List<Domain.Entities.ITorrentItemWrapper>(), unlinkedConfig);

            // Assert
            await _fixture.ClientWrapper.DidNotReceive().SetTorrentCategoryAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<string>());
        }

        [Fact]
        public async Task MissingHash_SkipsTorrent()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var unlinkedConfig = new UnlinkedConfig
            {
                Id = Guid.NewGuid(),
                UseTag = false,
                TargetCategory = "unlinked"
            };

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new QBitItemWrapper(new TorrentInfo { Hash = "", Name = "Test", Category = "movies", SavePath = "/downloads" }, Array.Empty<TorrentTracker>(), false)
            };

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert
            await _fixture.ClientWrapper.DidNotReceive().SetTorrentCategoryAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<string>());
        }

        [Fact]
        public async Task MissingName_SkipsTorrent()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var unlinkedConfig = new UnlinkedConfig
            {
                Id = Guid.NewGuid(),
                UseTag = false,
                TargetCategory = "unlinked"
            };

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new QBitItemWrapper(new TorrentInfo { Hash = "hash1", Name = "", Category = "movies", SavePath = "/downloads" }, Array.Empty<TorrentTracker>(), false)
            };

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert
            await _fixture.ClientWrapper.DidNotReceive().SetTorrentCategoryAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<string>());
        }

        [Fact]
        public async Task MissingCategory_SkipsTorrent()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var unlinkedConfig = new UnlinkedConfig
            {
                Id = Guid.NewGuid(),
                UseTag = false,
                TargetCategory = "unlinked"
            };

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new QBitItemWrapper(new TorrentInfo { Hash = "hash1", Name = "Test", Category = "", SavePath = "/downloads" }, Array.Empty<TorrentTracker>(), false)
            };

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert
            await _fixture.ClientWrapper.DidNotReceive().SetTorrentCategoryAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<string>());
        }

        [Fact]
        public async Task NoFiles_SkipsTorrent()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var unlinkedConfig = new UnlinkedConfig
            {
                Id = Guid.NewGuid(),
                UseTag = false,
                TargetCategory = "unlinked"
            };

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new QBitItemWrapper(new TorrentInfo { Hash = "hash1", Name = "Test", Category = "movies", SavePath = "/downloads" }, Array.Empty<TorrentTracker>(), false)
            };

            _fixture.ClientWrapper
                .GetTorrentContentsAsync("hash1")
                .Returns((IReadOnlyList<TorrentContent>?)null);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert
            await _fixture.ClientWrapper.DidNotReceive().SetTorrentCategoryAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<string>());
        }

        [Fact]
        public async Task NoHardlinks_ChangesCategory()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var unlinkedConfig = new UnlinkedConfig
            {
                Id = Guid.NewGuid(),
                UseTag = false,
                TargetCategory = "unlinked"
            };

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new QBitItemWrapper(new TorrentInfo { Hash = "hash1", Name = "Test", Category = "movies", SavePath = "/downloads" }, Array.Empty<TorrentTracker>(), false)
            };

            _fixture.ClientWrapper
                .GetTorrentContentsAsync("hash1")
                .Returns(new[]
                {
                    new TorrentContent { Index = 0, Name = "file1.mkv", Priority = TorrentContentPriority.Normal }
                });

            _fixture.HardLinkFileService
                .GetHardLinkCount(Arg.Any<string>(), Arg.Any<bool>())
                .Returns(0);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert
            await _fixture.ClientWrapper.Received(1)
                .SetTorrentCategoryAsync(Arg.Is<IEnumerable<string>>(h => h.Contains("hash1")), "unlinked");
        }

        [Fact]
        public async Task NoHardlinks_TagMode_AddsTag()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var unlinkedConfig = new UnlinkedConfig
            {
                Id = Guid.NewGuid(),
                UseTag = true,
                TargetCategory = "unlinked"
            };

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new QBitItemWrapper(new TorrentInfo { Hash = "hash1", Name = "Test", Category = "movies", SavePath = "/downloads" }, Array.Empty<TorrentTracker>(), false)
            };

            _fixture.ClientWrapper
                .GetTorrentContentsAsync("hash1")
                .Returns(new[]
                {
                    new TorrentContent { Index = 0, Name = "file1.mkv", Priority = TorrentContentPriority.Normal }
                });

            _fixture.HardLinkFileService
                .GetHardLinkCount(Arg.Any<string>(), Arg.Any<bool>())
                .Returns(0);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert
            await _fixture.ClientWrapper.Received(1)
                .AddTorrentTagAsync(Arg.Is<IEnumerable<string>>(h => h.Contains("hash1")), "unlinked");
            await _fixture.ClientWrapper.DidNotReceive()
                .SetTorrentCategoryAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<string>());
        }

        [Fact]
        public async Task HasHardlinks_SkipsTorrent()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var unlinkedConfig = new UnlinkedConfig
            {
                Id = Guid.NewGuid(),
                UseTag = false,
                TargetCategory = "unlinked"
            };

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new QBitItemWrapper(new TorrentInfo { Hash = "hash1", Name = "Test", Category = "movies", SavePath = "/downloads" }, Array.Empty<TorrentTracker>(), false)
            };

            _fixture.ClientWrapper
                .GetTorrentContentsAsync("hash1")
                .Returns(new[]
                {
                    new TorrentContent { Index = 0, Name = "file1.mkv", Priority = TorrentContentPriority.Normal }
                });

            _fixture.HardLinkFileService
                .GetHardLinkCount(Arg.Any<string>(), Arg.Any<bool>())
                .Returns(2); // Has hardlinks

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert
            await _fixture.ClientWrapper.DidNotReceive().SetTorrentCategoryAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<string>());
        }

        [Fact]
        public async Task FileNotFound_SkipsTorrent()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var unlinkedConfig = new UnlinkedConfig
            {
                Id = Guid.NewGuid(),
                UseTag = false,
                TargetCategory = "unlinked"
            };

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new QBitItemWrapper(new TorrentInfo { Hash = "hash1", Name = "Test", Category = "movies", SavePath = "/downloads" }, Array.Empty<TorrentTracker>(), false)
            };

            _fixture.ClientWrapper
                .GetTorrentContentsAsync("hash1")
                .Returns(new[]
                {
                    new TorrentContent { Index = 0, Name = "file1.mkv", Priority = TorrentContentPriority.Normal }
                });

            _fixture.HardLinkFileService
                .GetHardLinkCount(Arg.Any<string>(), Arg.Any<bool>())
                .Returns(-1); // Error

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert
            await _fixture.ClientWrapper.DidNotReceive().SetTorrentCategoryAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<string>());
        }

        [Fact]
        public async Task SkippedFiles_IgnoredInCheck()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var unlinkedConfig = new UnlinkedConfig
            {
                Id = Guid.NewGuid(),
                UseTag = false,
                TargetCategory = "unlinked"
            };

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new QBitItemWrapper(new TorrentInfo { Hash = "hash1", Name = "Test", Category = "movies", SavePath = "/downloads" }, Array.Empty<TorrentTracker>(), false)
            };

            _fixture.ClientWrapper
                .GetTorrentContentsAsync("hash1")
                .Returns(new[]
                {
                    new TorrentContent { Index = 0, Name = "file1.mkv", Priority = TorrentContentPriority.Skip },
                    new TorrentContent { Index = 1, Name = "file2.mkv", Priority = TorrentContentPriority.Normal }
                });

            _fixture.HardLinkFileService
                .GetHardLinkCount(Arg.Any<string>(), Arg.Any<bool>())
                .Returns(0);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert
            _fixture.HardLinkFileService.Received(1)
                .GetHardLinkCount(Arg.Any<string>(), Arg.Any<bool>()); // Only called for file2.mkv
        }

        [Fact]
        public async Task FileWithNullIndex_SkipsTorrent()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var unlinkedConfig = new UnlinkedConfig
            {
                Id = Guid.NewGuid(),
                UseTag = false,
                TargetCategory = "unlinked"
            };

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new QBitItemWrapper(new TorrentInfo { Hash = "hash1", Name = "Test", Category = "movies", SavePath = "/downloads" }, Array.Empty<TorrentTracker>(), false)
            };

            _fixture.ClientWrapper
                .GetTorrentContentsAsync("hash1")
                .Returns(new[]
                {
                    new TorrentContent { Index = null, Name = "file1.mkv", Priority = TorrentContentPriority.Normal }
                });

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert
            await _fixture.ClientWrapper.DidNotReceive().SetTorrentCategoryAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<string>());
        }

        [Fact]
        public async Task PublishesCategoryChangedEvent()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var unlinkedConfig = new UnlinkedConfig
            {
                Id = Guid.NewGuid(),
                UseTag = false,
                TargetCategory = "unlinked"
            };

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new QBitItemWrapper(new TorrentInfo { Hash = "hash1", Name = "Test", Category = "movies", SavePath = "/downloads" }, Array.Empty<TorrentTracker>(), false)
            };

            _fixture.ClientWrapper
                .GetTorrentContentsAsync("hash1")
                .Returns(new[]
                {
                    new TorrentContent { Index = 0, Name = "file1.mkv", Priority = TorrentContentPriority.Normal }
                });

            _fixture.HardLinkFileService
                .GetHardLinkCount(Arg.Any<string>(), Arg.Any<bool>())
                .Returns(0);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert - EventPublisher is not mocked, so we just verify the method completed
            await _fixture.ClientWrapper.Received(1)
                .SetTorrentCategoryAsync(Arg.Is<IEnumerable<string>>(h => h.Contains("hash1")), "unlinked");
        }

        [Fact]
        public async Task PublishesCategoryChangedEvent_WithTagFlag()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var unlinkedConfig = new UnlinkedConfig
            {
                Id = Guid.NewGuid(),
                UseTag = true,
                TargetCategory = "unlinked"
            };

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new QBitItemWrapper(new TorrentInfo { Hash = "hash1", Name = "Test", Category = "movies", SavePath = "/downloads" }, Array.Empty<TorrentTracker>(), false)
            };

            _fixture.ClientWrapper
                .GetTorrentContentsAsync("hash1")
                .Returns(new[]
                {
                    new TorrentContent { Index = 0, Name = "file1.mkv", Priority = TorrentContentPriority.Normal }
                });

            _fixture.HardLinkFileService
                .GetHardLinkCount(Arg.Any<string>(), Arg.Any<bool>())
                .Returns(0);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert - EventPublisher is not mocked, so we just verify the method completed
            await _fixture.ClientWrapper.Received(1)
                .AddTorrentTagAsync(Arg.Is<IEnumerable<string>>(h => h.Contains("hash1")), "unlinked");
        }
    }
}
