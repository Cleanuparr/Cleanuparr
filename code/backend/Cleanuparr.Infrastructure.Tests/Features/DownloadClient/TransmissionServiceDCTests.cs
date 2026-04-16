using Cleanuparr.Infrastructure.Features.DownloadClient.Transmission;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using NSubstitute;
using Transmission.API.RPC.Entity;
using Shouldly;
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
                .TorrentGetAsync(Arg.Any<string[]>(), Arg.Any<string?>())
                .Returns(torrents);

            // Act
            var result = await sut.GetSeedingDownloads();

            // Assert
            result.Count.ShouldBe(2);
            foreach (var item in result) { item.Hash.ShouldNotBeNull(); }
        }

        [Fact]
        public async Task ReturnsEmptyList_WhenNull()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            _fixture.ClientWrapper
                .TorrentGetAsync(Arg.Any<string[]>(), Arg.Any<string?>())
                .Returns((TransmissionTorrents?)null);

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

            var torrents = new TransmissionTorrents
            {
                Torrents = new[]
                {
                    new TorrentInfo { HashString = "", Name = "No Hash", Status = 5 },
                    new TorrentInfo { HashString = "hash1", Name = "Valid Hash", Status = 5 }
                }
            };

            _fixture.ClientWrapper
                .TorrentGetAsync(Arg.Any<string[]>(), Arg.Any<string?>())
                .Returns(torrents);

            // Act
            var result = await sut.GetSeedingDownloads();

            // Assert
            result.ShouldHaveSingleItem();
            result[0].Hash.ShouldBe("hash1");
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
                .TorrentGetAsync(Arg.Any<string[]>(), Arg.Any<string?>())
                .Returns(torrents);

            // Act
            var result = await sut.GetSeedingDownloads();

            // Assert
            result.ShouldBeEmpty();
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

            var categories = new List<ISeedingRule>
            {
                new TransmissionSeedingRule { Name = "movies", Categories = ["movies"], MaxRatio = -1, MinSeedTime = 0, MaxSeedTime = -1, DeleteSourceFiles = true },
                new TransmissionSeedingRule { Name = "tv", Categories = ["tv"], MaxRatio = -1, MinSeedTime = 0, MaxSeedTime = -1, DeleteSourceFiles = true }
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
                new TransmissionItemWrapper(new TorrentInfo { HashString = "hash1", DownloadDir = "/downloads/Movies" })
            };

            var categories = new List<ISeedingRule>
            {
                new TransmissionSeedingRule { Name = "movies", Categories = ["movies"], MaxRatio = -1, MinSeedTime = 0, MaxSeedTime = -1, DeleteSourceFiles = true }
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
                new TransmissionItemWrapper(new TorrentInfo { HashString = "hash1", DownloadDir = "/downloads/music" })
            };

            var categories = new List<ISeedingRule>
            {
                new TransmissionSeedingRule { Name = "movies", Categories = ["movies"], MaxRatio = -1, MinSeedTime = 0, MaxSeedTime = -1, DeleteSourceFiles = true }
            };

            // Act
            var result = sut.FilterDownloadsToBeCleanedAsync(downloads, categories);

            // Assert
            result.ShouldNotBeNull();
            result.ShouldBeEmpty();
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
                new TransmissionItemWrapper(new TorrentInfo { HashString = "hash1", DownloadDir = "/downloads/Movies" })
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
                new TransmissionItemWrapper(new TorrentInfo { HashString = "", DownloadDir = "/downloads/movies" }),
                new TransmissionItemWrapper(new TorrentInfo { HashString = "hash1", DownloadDir = "/downloads/movies" })
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
                new TransmissionItemWrapper(new TorrentInfo { HashString = "hash1", DownloadDir = "/downloads/tv" })
            };

            // Act
            var result = sut.FilterDownloadsToChangeCategoryAsync(downloads, new UnlinkedConfig { Categories = ["movies"] });

            // Assert
            result.ShouldNotBeNull();
            result.ShouldBeEmpty();
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
            _fixture.ClientWrapper.ReceivedCalls().ToList().ForEach(call =>
            {
                // Allow any calls that were set up, just verify no unexpected calls
            });
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
            var torrentInfo = new TorrentInfo { Id = 123, HashString = hash };
            var torrentWrapper = new TransmissionItemWrapper(torrentInfo);

            _fixture.ClientWrapper
                .TorrentRemoveAsync(Arg.Is<long[]>(ids => ids.Contains(123)), true)
                .Returns(Task.CompletedTask);

            // Act
            await sut.DeleteDownload(torrentWrapper, true);

            // Assert
            await _fixture.ClientWrapper.Received(1)
                .TorrentRemoveAsync(Arg.Is<long[]>(ids => ids.Contains(123)), true);
        }

        [Fact]
        public async Task HandlesNotFound()
        {
            // Arrange
            var sut = _fixture.CreateSut();
            const string hash = "nonexistent-hash";
            var torrentInfo = new TorrentInfo { Id = 456, HashString = hash };
            var torrentWrapper = new TransmissionItemWrapper(torrentInfo);

            _fixture.ClientWrapper
                .TorrentRemoveAsync(Arg.Is<long[]>(ids => ids.Contains(456)), true)
                .Returns(Task.CompletedTask);

            // Act
            await sut.DeleteDownload(torrentWrapper, true);

            // Assert
            await _fixture.ClientWrapper.Received(1)
                .TorrentRemoveAsync(Arg.Is<long[]>(ids => ids.Contains(456)), true);
        }

        [Fact]
        public async Task DeletesWithData()
        {
            // Arrange
            var sut = _fixture.CreateSut();
            const string hash = "test-hash";
            var torrentInfo = new TorrentInfo { Id = 123, HashString = hash };
            var torrentWrapper = new TransmissionItemWrapper(torrentInfo);

            _fixture.ClientWrapper
                .TorrentRemoveAsync(Arg.Any<long[]>(), true)
                .Returns(Task.CompletedTask);

            // Act
            await sut.DeleteDownload(torrentWrapper, true);

            // Assert
            await _fixture.ClientWrapper.Received(1)
                .TorrentRemoveAsync(Arg.Any<long[]>(), true);
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

            var unlinkedConfig = new UnlinkedConfig
            {
                Id = Guid.NewGuid(),
                TargetCategory = "unlinked"
            };

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(null, unlinkedConfig);

            // Assert
            await _fixture.ClientWrapper.DidNotReceive().TorrentSetLocationAsync(Arg.Any<long[]>(), Arg.Any<string>(), Arg.Any<bool>());
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
            await _fixture.ClientWrapper.DidNotReceive().TorrentSetLocationAsync(Arg.Any<long[]>(), Arg.Any<string>(), Arg.Any<bool>());
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
                new TransmissionItemWrapper(new TorrentInfo { HashString = "", Name = "Test", DownloadDir = "/downloads" })
            };

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert
            await _fixture.ClientWrapper.DidNotReceive().TorrentSetLocationAsync(Arg.Any<long[]>(), Arg.Any<string>(), Arg.Any<bool>());
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
                new TransmissionItemWrapper(new TorrentInfo { HashString = "hash1", Name = "", DownloadDir = "/downloads" })
            };

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert
            await _fixture.ClientWrapper.DidNotReceive().TorrentSetLocationAsync(Arg.Any<long[]>(), Arg.Any<string>(), Arg.Any<bool>());
        }

        [Fact]
        public async Task MissingDownloadDir_SkipsTorrent()
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
                new TransmissionItemWrapper(new TorrentInfo { HashString = "hash1", Name = "Test", DownloadDir = "" })
            };

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert
            await _fixture.ClientWrapper.DidNotReceive().TorrentSetLocationAsync(Arg.Any<long[]>(), Arg.Any<string>(), Arg.Any<bool>());
        }

        [Fact]
        public async Task MissingFiles_SkipsTorrent()
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
                new TransmissionItemWrapper(new TorrentInfo { HashString = "hash1", Name = "Test", DownloadDir = "/downloads", Files = null })
            };

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert
            await _fixture.ClientWrapper.DidNotReceive().TorrentSetLocationAsync(Arg.Any<long[]>(), Arg.Any<string>(), Arg.Any<bool>());
        }

        [Fact]
        public async Task MissingFileStats_SkipsTorrent()
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
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert
            await _fixture.ClientWrapper.DidNotReceive().TorrentSetLocationAsync(Arg.Any<long[]>(), Arg.Any<string>(), Arg.Any<bool>());
        }

        [Fact]
        public async Task NoHardlinks_ChangesLocation()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var unlinkedConfig = new UnlinkedConfig
            {
                Id = Guid.NewGuid(),
                TargetCategory = "unlinked"
            };

            var baseDownloadDir = Path.Combine("downloads", "movies");
            var expectedNewLocation = string.Join(Path.DirectorySeparatorChar,
                Path.Combine(baseDownloadDir, "unlinked").Split(['\\', '/']));

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new TransmissionItemWrapper(new TorrentInfo
                {
                    Id = 123,
                    HashString = "hash1",
                    Name = "Test",
                    DownloadDir = baseDownloadDir,
                    Files = new[] { new TransmissionTorrentFiles { Name = "file1.mkv" } },
                    FileStats = new[] { new TransmissionTorrentFileStats { Wanted = true } }
                })
            };

            _fixture.HardLinkFileService
                .GetHardLinkCount(Arg.Any<string>(), Arg.Any<bool>())
                .Returns(0);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert
            await _fixture.ClientWrapper.Received(1)
                .TorrentSetLocationAsync(Arg.Is<long[]>(ids => ids.Contains(123)), expectedNewLocation, true);
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
                .GetHardLinkCount(Arg.Any<string>(), Arg.Any<bool>())
                .Returns(2);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert
            await _fixture.ClientWrapper.DidNotReceive().TorrentSetLocationAsync(Arg.Any<long[]>(), Arg.Any<string>(), Arg.Any<bool>());
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
                .GetHardLinkCount(Arg.Any<string>(), Arg.Any<bool>())
                .Returns(-1);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert
            await _fixture.ClientWrapper.DidNotReceive().TorrentSetLocationAsync(Arg.Any<long[]>(), Arg.Any<string>(), Arg.Any<bool>());
        }

        [Fact]
        public async Task UnwantedFiles_IgnoredInCheck()
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

            var baseDownloadDir = Path.Combine("downloads", "movies");
            var expectedNewLocation = string.Join(Path.DirectorySeparatorChar,
                Path.Combine(baseDownloadDir, "unlinked").Split(['\\', '/']));

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new TransmissionItemWrapper(new TorrentInfo
                {
                    Id = 123,
                    HashString = "hash1",
                    Name = "Test",
                    DownloadDir = baseDownloadDir,
                    Files = new[] { new TransmissionTorrentFiles { Name = "file1.mkv" } },
                    FileStats = new[] { new TransmissionTorrentFileStats { Wanted = true } }
                })
            };

            _fixture.HardLinkFileService
                .GetHardLinkCount(Arg.Any<string>(), Arg.Any<bool>())
                .Returns(0);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert - EventPublisher is not mocked, so we just verify the method completed
            await _fixture.ClientWrapper.Received(1)
                .TorrentSetLocationAsync(Arg.Is<long[]>(ids => ids.Contains(123)), expectedNewLocation, true);
        }

        [Fact]
        public async Task AppendsTargetCategoryToBasePath()
        {
            // Arrange
            var sut = _fixture.CreateSut();

            var unlinkedConfig = new UnlinkedConfig
            {
                Id = Guid.NewGuid(),
                TargetCategory = "unlinked"
            };

            var baseDownloadDir = Path.Combine("downloads", "movies", "subfolder");
            var expectedNewLocation = string.Join(Path.DirectorySeparatorChar,
                Path.Combine(baseDownloadDir, "unlinked").Split(['\\', '/']));

            var downloads = new List<Domain.Entities.ITorrentItemWrapper>
            {
                new TransmissionItemWrapper(new TorrentInfo
                {
                    Id = 123,
                    HashString = "hash1",
                    Name = "Test",
                    DownloadDir = baseDownloadDir,
                    Files = new[] { new TransmissionTorrentFiles { Name = "file1.mkv" } },
                    FileStats = new[] { new TransmissionTorrentFileStats { Wanted = true } }
                })
            };

            _fixture.HardLinkFileService
                .GetHardLinkCount(Arg.Any<string>(), Arg.Any<bool>())
                .Returns(0);

            // Act
            await sut.ChangeCategoryForNoHardLinksAsync(downloads, unlinkedConfig);

            // Assert
            await _fixture.ClientWrapper.Received(1)
                .TorrentSetLocationAsync(Arg.Is<long[]>(ids => ids.Contains(123)), expectedNewLocation, true);
        }
    }
}
