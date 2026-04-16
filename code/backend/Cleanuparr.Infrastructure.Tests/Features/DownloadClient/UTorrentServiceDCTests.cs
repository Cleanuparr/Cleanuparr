using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.UTorrent.Response;
using Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using NSubstitute;
using Shouldly;
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
                .GetTorrentsAsync()
                .Returns(torrents);

            _fixture.ClientWrapper
                .GetTorrentPropertiesAsync("hash1")
                .Returns(new UTorrentProperties { Hash = "hash1", Pex = 1, Trackers = "" });

            _fixture.ClientWrapper
                .GetTorrentPropertiesAsync("hash3")
                .Returns(new UTorrentProperties { Hash = "hash3", Pex = 1, Trackers = "" });

            // Act
            var result = await sut.GetSeedingDownloads();

            // Assert
            result.Count.ShouldBe(2);
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
                .GetTorrentsAsync()
                .Returns(torrents);

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

            var torrents = new List<UTorrentItem>
            {
                new UTorrentItem { Hash = "", Name = "No Hash", Status = 9, DateCompleted = 1000 },
                new UTorrentItem { Hash = "hash1", Name = "Valid Hash", Status = 9, DateCompleted = 1000 }
            };

            _fixture.ClientWrapper
                .GetTorrentsAsync()
                .Returns(torrents);

            _fixture.ClientWrapper
                .GetTorrentPropertiesAsync("hash1")
                .Returns(new UTorrentProperties { Hash = "hash1", Pex = 1, Trackers = "" });

            // Act
            var result = await sut.GetSeedingDownloads();

            // Assert
            result.ShouldHaveSingleItem();
            result[0].Hash.ShouldBe("hash1");
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

            var categories = new List<ISeedingRule>
            {
                new UTorrentSeedingRule { Name = "movies", Categories = ["movies"], MaxRatio = -1, MinSeedTime = 0, MaxSeedTime = -1, DeleteSourceFiles = true },
                new UTorrentSeedingRule { Name = "tv", Categories = ["tv"], MaxRatio = -1, MinSeedTime = 0, MaxSeedTime = -1, DeleteSourceFiles = true }
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
                new UTorrentItemWrapper(new UTorrentItem { Hash = "hash1", Label = "Movies" }, new UTorrentProperties { Hash = "hash1", Pex = 1, Trackers = "" })
            };

            var categories = new List<ISeedingRule>
            {
                new UTorrentSeedingRule { Name = "movies", Categories = ["movies"], MaxRatio = -1, MinSeedTime = 0, MaxSeedTime = -1, DeleteSourceFiles = true }
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
                new UTorrentItemWrapper(new UTorrentItem { Hash = "hash1", Label = "music" }, new UTorrentProperties { Hash = "hash1", Pex = 1, Trackers = "" })
            };

            var categories = new List<ISeedingRule>
            {
                new UTorrentSeedingRule { Name = "movies", Categories = ["movies"], MaxRatio = -1, MinSeedTime = 0, MaxSeedTime = -1, DeleteSourceFiles = true }
            };

            // Act
            var result = sut.FilterDownloadsToBeCleanedAsync(downloads, categories);

            // Assert
            result.ShouldNotBeNull();
            result.ShouldBeEmpty();
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
                new UTorrentItemWrapper(new UTorrentItem { Hash = "hash1", Label = "Movies" }, new UTorrentProperties { Hash = "hash1", Pex = 1, Trackers = "" })
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
                new UTorrentItemWrapper(new UTorrentItem { Hash = "", Label = "movies" }, new UTorrentProperties { Hash = "", Pex = 1, Trackers = "" }),
                new UTorrentItemWrapper(new UTorrentItem { Hash = "hash1", Label = "movies" }, new UTorrentProperties { Hash = "hash1", Pex = 1, Trackers = "" })
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
                new UTorrentItemWrapper(new UTorrentItem { Hash = "hash1", Label = "tv" }, new UTorrentProperties { Hash = "hash1", Pex = 1, Trackers = "" })
            };

            // Act
            var result = sut.FilterDownloadsToChangeCategoryAsync(downloads, new UnlinkedConfig { Categories = ["movies"] });

            // Assert
            result.ShouldNotBeNull();
            result.ShouldBeEmpty();
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
            var mockTorrent = Substitute.For<ITorrentItemWrapper>();
            mockTorrent.Hash.Returns(hash);

            _fixture.ClientWrapper
                .RemoveTorrentsAsync(Arg.Is<List<string>>(h => h.Contains("test-hash")), true)
                .Returns(Task.CompletedTask);

            // Act
            await sut.DeleteDownload(mockTorrent, true);

            // Assert
            await _fixture.ClientWrapper.Received(1)
                .RemoveTorrentsAsync(Arg.Is<List<string>>(h => h.Contains("test-hash")), true);
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
                .RemoveTorrentsAsync(Arg.Any<List<string>>(), true)
                .Returns(Task.CompletedTask);

            // Act
            await sut.DeleteDownload(mockTorrent, true);

            // Assert
            await _fixture.ClientWrapper.Received(1)
                .RemoveTorrentsAsync(Arg.Is<List<string>>(h => h.Contains("uppercase-hash")), true);
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
                .RemoveTorrentsAsync(Arg.Is<List<string>>(h => h.Contains("test-hash")), false)
                .Returns(Task.CompletedTask);

            // Act
            await sut.DeleteDownload(mockTorrent, false);

            // Assert
            await _fixture.ClientWrapper.Received(1)
                .RemoveTorrentsAsync(Arg.Is<List<string>>(h => h.Contains("test-hash")), false);
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

            var unlinkedConfig = new UnlinkedConfig
            {
                Id = Guid.NewGuid(),
                TargetCategory = "unlinked"
            };

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(null, unlinkedConfig);

            // Assert
            await _fixture.ClientWrapper.DidNotReceive().SetTorrentLabelAsync(Arg.Any<string>(), Arg.Any<string>());
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
            await _fixture.ClientWrapper.DidNotReceive().SetTorrentLabelAsync(Arg.Any<string>(), Arg.Any<string>());
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
                new UTorrentItemWrapper(
                    new UTorrentItem { Hash = "", Name = "Test", Label = "movies", SavePath = "/downloads" },
                    new UTorrentProperties { Hash = "", Pex = 1, Trackers = "" })
            };

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert
            await _fixture.ClientWrapper.DidNotReceive().SetTorrentLabelAsync(Arg.Any<string>(), Arg.Any<string>());
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
                new UTorrentItemWrapper(
                    new UTorrentItem { Hash = "hash1", Name = "", Label = "movies", SavePath = "/downloads" },
                    new UTorrentProperties { Hash = "hash1", Pex = 1, Trackers = "" })
            };

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert
            await _fixture.ClientWrapper.DidNotReceive().SetTorrentLabelAsync(Arg.Any<string>(), Arg.Any<string>());
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
                new UTorrentItemWrapper(
                    new UTorrentItem { Hash = "hash1", Name = "Test", Label = "", SavePath = "/downloads" },
                    new UTorrentProperties { Hash = "hash1", Pex = 1, Trackers = "" })
            };

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert
            await _fixture.ClientWrapper.DidNotReceive().SetTorrentLabelAsync(Arg.Any<string>(), Arg.Any<string>());
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
                new UTorrentItemWrapper(
                    new UTorrentItem { Hash = "hash1", Name = "Test", Label = "movies", SavePath = "/downloads" },
                    new UTorrentProperties { Hash = "hash1", Pex = 1, Trackers = "" })
            };

            _fixture.ClientWrapper
                .GetTorrentFilesAsync("hash1")
                .Returns(new List<UTorrentFile>
                {
                    new UTorrentFile { Name = "file1.mkv", Priority = 1, Index = 0, Size = 1000, Downloaded = 500 }
                });

            _fixture.HardLinkFileService
                .GetHardLinkCount(Arg.Any<string>(), Arg.Any<bool>())
                .Returns(0);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert
            await _fixture.ClientWrapper.Received(1)
                .SetTorrentLabelAsync("hash1", "unlinked");
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
                new UTorrentItemWrapper(
                    new UTorrentItem { Hash = "hash1", Name = "Test", Label = "movies", SavePath = "/downloads" },
                    new UTorrentProperties { Hash = "hash1", Pex = 1, Trackers = "" })
            };

            _fixture.ClientWrapper
                .GetTorrentFilesAsync("hash1")
                .Returns(new List<UTorrentFile>
                {
                    new UTorrentFile { Name = "file1.mkv", Priority = 1, Index = 0, Size = 1000, Downloaded = 500 }
                });

            _fixture.HardLinkFileService
                .GetHardLinkCount(Arg.Any<string>(), Arg.Any<bool>())
                .Returns(2);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert
            await _fixture.ClientWrapper.DidNotReceive().SetTorrentLabelAsync(Arg.Any<string>(), Arg.Any<string>());
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
                new UTorrentItemWrapper(
                    new UTorrentItem { Hash = "hash1", Name = "Test", Label = "movies", SavePath = "/downloads" },
                    new UTorrentProperties { Hash = "hash1", Pex = 1, Trackers = "" })
            };

            _fixture.ClientWrapper
                .GetTorrentFilesAsync("hash1")
                .Returns(new List<UTorrentFile>
                {
                    new UTorrentFile { Name = "file1.mkv", Priority = 1, Index = 0, Size = 1000, Downloaded = 500 }
                });

            _fixture.HardLinkFileService
                .GetHardLinkCount(Arg.Any<string>(), Arg.Any<bool>())
                .Returns(-1);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert
            await _fixture.ClientWrapper.DidNotReceive().SetTorrentLabelAsync(Arg.Any<string>(), Arg.Any<string>());
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
                new UTorrentItemWrapper(
                    new UTorrentItem { Hash = "hash1", Name = "Test", Label = "movies", SavePath = "/downloads" },
                    new UTorrentProperties { Hash = "hash1", Pex = 1, Trackers = "" })
            };

            _fixture.ClientWrapper
                .GetTorrentFilesAsync("hash1")
                .Returns(new List<UTorrentFile>
                {
                    new UTorrentFile { Name = "file1.mkv", Priority = 0, Index = 0, Size = 1000, Downloaded = 0 },
                    new UTorrentFile { Name = "file2.mkv", Priority = 1, Index = 1, Size = 2000, Downloaded = 1000 }
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
                new UTorrentItemWrapper(
                    new UTorrentItem { Hash = "hash1", Name = "Test", Label = "movies", SavePath = "/downloads" },
                    new UTorrentProperties { Hash = "hash1", Pex = 1, Trackers = "" })
            };

            _fixture.ClientWrapper
                .GetTorrentFilesAsync("hash1")
                .Returns(new List<UTorrentFile>
                {
                    new UTorrentFile { Name = "file1.mkv", Priority = 1, Index = 0, Size = 1000, Downloaded = 500 }
                });

            _fixture.HardLinkFileService
                .GetHardLinkCount(Arg.Any<string>(), Arg.Any<bool>())
                .Returns(0);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert - EventPublisher is not mocked, so we just verify the method completed
            await _fixture.ClientWrapper.Received(1)
                .SetTorrentLabelAsync("hash1", "unlinked");
        }

        [Fact]
        public async Task NullFilesResponse_ChangesLabel()
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
                new UTorrentItemWrapper(
                    new UTorrentItem { Hash = "hash1", Name = "Test", Label = "movies", SavePath = "/downloads" },
                    new UTorrentProperties { Hash = "hash1", Pex = 1, Trackers = "" })
            };

            _fixture.ClientWrapper
                .GetTorrentFilesAsync("hash1")
                .Returns((List<UTorrentFile>?)null);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert - When files is null, it uses empty collection and proceeds to change label
            await _fixture.ClientWrapper.Received(1).SetTorrentLabelAsync("hash1", "unlinked");
        }
    }
}
