using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.DownloadClient.QBittorrent;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Moq;
using Newtonsoft.Json.Linq;
using QBittorrent.Client;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient;

public class QBitServiceTests : IClassFixture<QBitServiceFixture>
{
    private readonly QBitServiceFixture _fixture;

    public QBitServiceTests(QBitServiceFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetMocks();
    }

    public class ShouldRemoveFromArrQueueAsync_BasicScenarios : QBitServiceTests
    {
        public ShouldRemoveFromArrQueueAsync_BasicScenarios(QBitServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task TorrentNotFound_ReturnsEmptyResult()
        {
            // Arrange
            const string hash = "nonexistent";
            var sut = _fixture.CreateSut();

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentListAsync(It.Is<TorrentListQuery>(q => q.Hashes != null && q.Hashes.Contains(hash))))
                .ReturnsAsync(Array.Empty<TorrentInfo>());

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            Assert.False(result.Found);
            Assert.False(result.ShouldRemove);
            Assert.Equal(DeleteReason.None, result.DeleteReason);
        }

        [Fact]
        public async Task TorrentIsIgnored_ReturnsEmptyResult_WithFound()
        {
            // Arrange
            const string hash = "test-hash";
            const string ignoredCategory = "ignored-category";
            var sut = _fixture.CreateSut();

            var torrentInfo = new TorrentInfo
            {
                Hash = hash,
                Name = "Test Torrent",
                Category = ignoredCategory,
                State = TorrentState.Downloading
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentListAsync(It.Is<TorrentListQuery>(q => q.Hashes != null && q.Hashes.Contains(hash))))
                .ReturnsAsync(new[] { torrentInfo });

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentTrackersAsync(hash))
                .ReturnsAsync(Array.Empty<TorrentTracker>());

            var properties = new TorrentProperties
            {
                AdditionalData = new Dictionary<string, JToken>
                {
                    { "is_private", JToken.FromObject(false) }
                }
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentPropertiesAsync(hash))
                .ReturnsAsync(properties);

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, new[] { ignoredCategory });

            // Assert
            Assert.True(result.Found);
            Assert.False(result.ShouldRemove);
        }

        [Fact]
        public async Task TorrentFound_SetsIsPrivateCorrectly_WhenPrivate()
        {
            // Arrange
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            var torrentInfo = new TorrentInfo
            {
                Hash = hash,
                Name = "Test Torrent",
                State = TorrentState.Downloading,
                DownloadSpeed = 1000
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentListAsync(It.Is<TorrentListQuery>(q => q.Hashes != null && q.Hashes.Contains(hash))))
                .ReturnsAsync(new[] { torrentInfo });

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentTrackersAsync(hash))
                .ReturnsAsync(Array.Empty<TorrentTracker>());

            var properties = new TorrentProperties
            {
                AdditionalData = new Dictionary<string, JToken>
                {
                    { "is_private", JToken.FromObject(true) }
                }
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentPropertiesAsync(hash))
                .ReturnsAsync(properties);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentContentsAsync(hash))
                .ReturnsAsync(new[]
                {
                    new TorrentContent { Index = 0, Priority = TorrentContentPriority.Normal }
                });

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateSlowRulesAsync(It.IsAny<QBitItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateStallRulesAsync(It.IsAny<QBitItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            Assert.True(result.Found);
            Assert.True(result.IsPrivate);
        }

        [Fact]
        public async Task TorrentFound_SetsIsPrivateCorrectly_WhenPublic()
        {
            // Arrange
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            var torrentInfo = new TorrentInfo
            {
                Hash = hash,
                Name = "Test Torrent",
                State = TorrentState.Downloading,
                DownloadSpeed = 1000
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentListAsync(It.Is<TorrentListQuery>(q => q.Hashes != null && q.Hashes.Contains(hash))))
                .ReturnsAsync(new[] { torrentInfo });

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentTrackersAsync(hash))
                .ReturnsAsync(Array.Empty<TorrentTracker>());

            var properties = new TorrentProperties
            {
                AdditionalData = new Dictionary<string, JToken>
                {
                    { "is_private", JToken.FromObject(false) }
                }
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentPropertiesAsync(hash))
                .ReturnsAsync(properties);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentContentsAsync(hash))
                .ReturnsAsync(new[]
                {
                    new TorrentContent { Index = 0, Priority = TorrentContentPriority.Normal }
                });

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateSlowRulesAsync(It.IsAny<QBitItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateStallRulesAsync(It.IsAny<QBitItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            Assert.True(result.Found);
            Assert.False(result.IsPrivate);
        }

        [Fact]
        public async Task TorrentPropertiesNotFound_ReturnsEmptyResult()
        {
            // Arrange
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            var torrentInfo = new TorrentInfo
            {
                Hash = hash,
                Name = "Test Torrent",
                State = TorrentState.Downloading
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentListAsync(It.Is<TorrentListQuery>(q => q.Hashes != null && q.Hashes.Contains(hash))))
                .ReturnsAsync(new[] { torrentInfo });

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentTrackersAsync(hash))
                .ReturnsAsync(Array.Empty<TorrentTracker>());

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentPropertiesAsync(hash))
                .ReturnsAsync((TorrentProperties?)null); // Properties not found

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            Assert.False(result.Found);
            Assert.False(result.ShouldRemove);
            Assert.False(result.IsPrivate);
            Assert.Equal(DeleteReason.None, result.DeleteReason);
            Assert.False(result.DeleteFromClient);
        }
    }

    public class ShouldRemoveFromArrQueueAsync_AllFilesSkippedScenarios : QBitServiceTests
    {
        public ShouldRemoveFromArrQueueAsync_AllFilesSkippedScenarios(QBitServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task AllFilesSkippedByQBit_WithNoDownload_DeletesFromClient()
        {
            // Arrange
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            var torrentInfo = new TorrentInfo
            {
                Hash = hash,
                Name = "Test Torrent",
                State = TorrentState.Downloading,
                CompletionOn = DateTime.UtcNow,
                Downloaded = 0 // No data downloaded
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentListAsync(It.Is<TorrentListQuery>(q => q.Hashes != null && q.Hashes.Contains(hash))))
                .ReturnsAsync(new[] { torrentInfo });

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentTrackersAsync(hash))
                .ReturnsAsync(Array.Empty<TorrentTracker>());

            var properties = new TorrentProperties
            {
                AdditionalData = new Dictionary<string, JToken>
                {
                    { "is_private", JToken.FromObject(false) }
                }
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentPropertiesAsync(hash))
                .ReturnsAsync(properties);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentContentsAsync(hash))
                .ReturnsAsync(new[]
                {
                    new TorrentContent { Index = 0, Priority = TorrentContentPriority.Skip },
                    new TorrentContent { Index = 1, Priority = TorrentContentPriority.Skip }
                });

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            Assert.True(result.ShouldRemove);
            Assert.Equal(DeleteReason.AllFilesSkippedByQBit, result.DeleteReason);
            Assert.True(result.DeleteFromClient);
        }

        [Fact]
        public async Task AllFilesSkippedByUser_DeletesFromClient()
        {
            // Arrange
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            var torrentInfo = new TorrentInfo
            {
                Hash = hash,
                Name = "Test Torrent",
                State = TorrentState.Downloading,
                Downloaded = 1000 // Some data downloaded
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentListAsync(It.Is<TorrentListQuery>(q => q.Hashes != null && q.Hashes.Contains(hash))))
                .ReturnsAsync(new[] { torrentInfo });

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentTrackersAsync(hash))
                .ReturnsAsync(Array.Empty<TorrentTracker>());

            var properties = new TorrentProperties
            {
                AdditionalData = new Dictionary<string, JToken>
                {
                    { "is_private", JToken.FromObject(false) }
                }
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentPropertiesAsync(hash))
                .ReturnsAsync(properties);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentContentsAsync(hash))
                .ReturnsAsync(new[]
                {
                    new TorrentContent { Index = 0, Priority = TorrentContentPriority.Skip },
                    new TorrentContent { Index = 1, Priority = TorrentContentPriority.Skip }
                });

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            Assert.True(result.ShouldRemove);
            Assert.Equal(DeleteReason.AllFilesSkipped, result.DeleteReason);
            Assert.True(result.DeleteFromClient);
        }

        [Fact]
        public async Task SomeFilesWanted_DoesNotRemove()
        {
            // Arrange
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            var torrentInfo = new TorrentInfo
            {
                Hash = hash,
                Name = "Test Torrent",
                State = TorrentState.Downloading,
                DownloadSpeed = 1000
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentListAsync(It.Is<TorrentListQuery>(q => q.Hashes != null && q.Hashes.Contains(hash))))
                .ReturnsAsync(new[] { torrentInfo });

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentTrackersAsync(hash))
                .ReturnsAsync(Array.Empty<TorrentTracker>());

            var properties = new TorrentProperties
            {
                AdditionalData = new Dictionary<string, JToken>
                {
                    { "is_private", JToken.FromObject(false) }
                }
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentPropertiesAsync(hash))
                .ReturnsAsync(properties);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentContentsAsync(hash))
                .ReturnsAsync(new[]
                {
                    new TorrentContent { Index = 0, Priority = TorrentContentPriority.Skip },
                    new TorrentContent { Index = 1, Priority = TorrentContentPriority.Normal } // At least one wanted
                });

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateSlowRulesAsync(It.IsAny<QBitItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateStallRulesAsync(It.IsAny<QBitItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            Assert.False(result.ShouldRemove);
        }
    }

    public class ShouldRemoveFromArrQueueAsync_MetadataDownloadingScenarios : QBitServiceTests
    {
        public ShouldRemoveFromArrQueueAsync_MetadataDownloadingScenarios(QBitServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task DownloadingMetadata_WithStrikesEnabled_IncreasesStrikes()
        {
            // Arrange
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            var queueCleanerConfig = new QueueCleanerConfig
            {
                Id = Guid.NewGuid(),
                DownloadingMetadataMaxStrikes = 3
            };

            ContextProvider.Set(nameof(QueueCleanerConfig), queueCleanerConfig);

            var torrentInfo = new TorrentInfo
            {
                Hash = hash,
                Name = "Test Torrent",
                State = TorrentState.FetchingMetadata // Metadata downloading state
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentListAsync(It.Is<TorrentListQuery>(q => q.Hashes != null && q.Hashes.Contains(hash))))
                .ReturnsAsync(new[] { torrentInfo });

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentTrackersAsync(hash))
                .ReturnsAsync(Array.Empty<TorrentTracker>());

            var properties = new TorrentProperties
            {
                AdditionalData = new Dictionary<string, JToken>
                {
                    { "is_private", JToken.FromObject(false) }
                }
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentPropertiesAsync(hash))
                .ReturnsAsync(properties);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentContentsAsync(hash))
                .ReturnsAsync(new[]
                {
                    new TorrentContent { Index = 0, Priority = TorrentContentPriority.Normal }
                });

            _fixture.Striker
                .Setup(x => x.StrikeAndCheckLimit(hash, It.IsAny<string>(), (ushort)3, StrikeType.DownloadingMetadata, It.IsAny<long?>()))
                .ReturnsAsync(false);

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            Assert.False(result.ShouldRemove);
            _fixture.Striker.Verify(
                x => x.StrikeAndCheckLimit(hash, It.IsAny<string>(), (ushort)3, StrikeType.DownloadingMetadata, It.IsAny<long?>()),
                Times.Once);
        }

        [Fact]
        public async Task DownloadingMetadata_ExceedsMaxStrikes_RemovesFromQueue()
        {
            // Arrange
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            var queueCleanerConfig = new QueueCleanerConfig
            {
                Id = Guid.NewGuid(),
                DownloadingMetadataMaxStrikes = 3
            };

            ContextProvider.Set(nameof(QueueCleanerConfig), queueCleanerConfig);

            var torrentInfo = new TorrentInfo
            {
                Hash = hash,
                Name = "Test Torrent",
                State = TorrentState.FetchingMetadata
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentListAsync(It.Is<TorrentListQuery>(q => q.Hashes != null && q.Hashes.Contains(hash))))
                .ReturnsAsync(new[] { torrentInfo });

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentTrackersAsync(hash))
                .ReturnsAsync(Array.Empty<TorrentTracker>());

            var properties = new TorrentProperties
            {
                AdditionalData = new Dictionary<string, JToken>
                {
                    { "is_private", JToken.FromObject(false) }
                }
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentPropertiesAsync(hash))
                .ReturnsAsync(properties);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentContentsAsync(hash))
                .ReturnsAsync(new[]
                {
                    new TorrentContent { Index = 0, Priority = TorrentContentPriority.Normal }
                });

            _fixture.Striker
                .Setup(x => x.StrikeAndCheckLimit(hash, It.IsAny<string>(), (ushort)3, StrikeType.DownloadingMetadata, It.IsAny<long?>()))
                .ReturnsAsync(true); // Strike limit exceeded

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            Assert.True(result.ShouldRemove);
            Assert.Equal(DeleteReason.DownloadingMetadata, result.DeleteReason);
            Assert.True(result.DeleteFromClient);
        }

        [Fact]
        public async Task DownloadingMetadata_WithStrikesDisabled_DoesNotRemove()
        {
            // Arrange
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            var queueCleanerConfig = new QueueCleanerConfig
            {
                Id = Guid.NewGuid(),
                DownloadingMetadataMaxStrikes = 0 // Disabled
            };

            ContextProvider.Set(nameof(QueueCleanerConfig), queueCleanerConfig);

            var torrentInfo = new TorrentInfo
            {
                Hash = hash,
                Name = "Test Torrent",
                State = TorrentState.FetchingMetadata
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentListAsync(It.Is<TorrentListQuery>(q => q.Hashes != null && q.Hashes.Contains(hash))))
                .ReturnsAsync(new[] { torrentInfo });

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentTrackersAsync(hash))
                .ReturnsAsync(Array.Empty<TorrentTracker>());

            var properties = new TorrentProperties
            {
                AdditionalData = new Dictionary<string, JToken>
                {
                    { "is_private", JToken.FromObject(false) }
                }
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentPropertiesAsync(hash))
                .ReturnsAsync(properties);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentContentsAsync(hash))
                .ReturnsAsync(new[]
                {
                    new TorrentContent { Index = 0, Priority = TorrentContentPriority.Normal }
                });

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            Assert.False(result.ShouldRemove);
            _fixture.Striker.Verify(
                x => x.StrikeAndCheckLimit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ushort>(), It.IsAny<StrikeType>(), It.IsAny<long?>()),
                Times.Never);
        }
    }

    public class ShouldRemoveFromArrQueueAsync_SlowDownloadScenarios : QBitServiceTests
    {
        public ShouldRemoveFromArrQueueAsync_SlowDownloadScenarios(QBitServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task SlowDownload_NotInDownloadingState_SkipsCheck()
        {
            // Arrange
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            var torrentInfo = new TorrentInfo
            {
                Hash = hash,
                Name = "Test Torrent",
                State = TorrentState.Uploading, // Not downloading
                DownloadSpeed = 100
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentListAsync(It.Is<TorrentListQuery>(q => q.Hashes != null && q.Hashes.Contains(hash))))
                .ReturnsAsync(new[] { torrentInfo });

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentTrackersAsync(hash))
                .ReturnsAsync(Array.Empty<TorrentTracker>());

            var properties = new TorrentProperties
            {
                AdditionalData = new Dictionary<string, JToken>
                {
                    { "is_private", JToken.FromObject(false) }
                }
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentPropertiesAsync(hash))
                .ReturnsAsync(properties);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentContentsAsync(hash))
                .ReturnsAsync(new[]
                {
                    new TorrentContent { Index = 0, Priority = TorrentContentPriority.Normal }
                });

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateStallRulesAsync(It.IsAny<QBitItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            Assert.False(result.ShouldRemove);
            _fixture.RuleEvaluator.Verify(
                x => x.EvaluateSlowRulesAsync(It.IsAny<QBitItemWrapper>()),
                Times.Never);
        }

        [Fact]
        public async Task SlowDownload_ZeroSpeed_SkipsCheck()
        {
            // Arrange
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            var torrentInfo = new TorrentInfo
            {
                Hash = hash,
                Name = "Test Torrent",
                State = TorrentState.Downloading,
                DownloadSpeed = 0 // Zero speed
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentListAsync(It.Is<TorrentListQuery>(q => q.Hashes != null && q.Hashes.Contains(hash))))
                .ReturnsAsync(new[] { torrentInfo });

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentTrackersAsync(hash))
                .ReturnsAsync(Array.Empty<TorrentTracker>());

            var properties = new TorrentProperties
            {
                AdditionalData = new Dictionary<string, JToken>
                {
                    { "is_private", JToken.FromObject(false) }
                }
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentPropertiesAsync(hash))
                .ReturnsAsync(properties);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentContentsAsync(hash))
                .ReturnsAsync(new[]
                {
                    new TorrentContent { Index = 0, Priority = TorrentContentPriority.Normal }
                });

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateStallRulesAsync(It.IsAny<QBitItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            Assert.False(result.ShouldRemove);
            _fixture.RuleEvaluator.Verify(
                x => x.EvaluateSlowRulesAsync(It.IsAny<QBitItemWrapper>()),
                Times.Never);
        }

        [Fact]
        public async Task SlowDownload_MatchesRule_RemovesFromQueue()
        {
            // Arrange
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            var torrentInfo = new TorrentInfo
            {
                Hash = hash,
                Name = "Test Torrent",
                State = TorrentState.Downloading,
                DownloadSpeed = 1000 // Some speed
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentListAsync(It.Is<TorrentListQuery>(q => q.Hashes != null && q.Hashes.Contains(hash))))
                .ReturnsAsync(new[] { torrentInfo });

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentTrackersAsync(hash))
                .ReturnsAsync(Array.Empty<TorrentTracker>());

            var properties = new TorrentProperties
            {
                AdditionalData = new Dictionary<string, JToken>
                {
                    { "is_private", JToken.FromObject(false) }
                }
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentPropertiesAsync(hash))
                .ReturnsAsync(properties);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentContentsAsync(hash))
                .ReturnsAsync(new[]
                {
                    new TorrentContent { Index = 0, Priority = TorrentContentPriority.Normal }
                });

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateSlowRulesAsync(It.IsAny<QBitItemWrapper>()))
                .ReturnsAsync((true, DeleteReason.SlowSpeed, true)); // Rule matched

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            Assert.True(result.ShouldRemove);
            Assert.Equal(DeleteReason.SlowSpeed, result.DeleteReason);
            Assert.True(result.DeleteFromClient);
        }
    }

    public class ShouldRemoveFromArrQueueAsync_StalledDownloadScenarios : QBitServiceTests
    {
        public ShouldRemoveFromArrQueueAsync_StalledDownloadScenarios(QBitServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task StalledDownload_NotInStalledState_SkipsCheck()
        {
            // Arrange
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            var torrentInfo = new TorrentInfo
            {
                Hash = hash,
                Name = "Test Torrent",
                State = TorrentState.Downloading, // Not stalled
                DownloadSpeed = 1000
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentListAsync(It.Is<TorrentListQuery>(q => q.Hashes != null && q.Hashes.Contains(hash))))
                .ReturnsAsync(new[] { torrentInfo });

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentTrackersAsync(hash))
                .ReturnsAsync(Array.Empty<TorrentTracker>());

            var properties = new TorrentProperties
            {
                AdditionalData = new Dictionary<string, JToken>
                {
                    { "is_private", JToken.FromObject(false) }
                }
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentPropertiesAsync(hash))
                .ReturnsAsync(properties);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentContentsAsync(hash))
                .ReturnsAsync(new[]
                {
                    new TorrentContent { Index = 0, Priority = TorrentContentPriority.Normal }
                });

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateSlowRulesAsync(It.IsAny<QBitItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            Assert.False(result.ShouldRemove);
            _fixture.RuleEvaluator.Verify(
                x => x.EvaluateStallRulesAsync(It.IsAny<QBitItemWrapper>()),
                Times.Never);
        }

        [Fact]
        public async Task StalledDownload_MatchesRule_RemovesFromQueue()
        {
            // Arrange
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            var torrentInfo = new TorrentInfo
            {
                Hash = hash,
                Name = "Test Torrent",
                State = TorrentState.StalledDownload // Stalled
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentListAsync(It.Is<TorrentListQuery>(q => q.Hashes != null && q.Hashes.Contains(hash))))
                .ReturnsAsync(new[] { torrentInfo });

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentTrackersAsync(hash))
                .ReturnsAsync(Array.Empty<TorrentTracker>());

            var properties = new TorrentProperties
            {
                AdditionalData = new Dictionary<string, JToken>
                {
                    { "is_private", JToken.FromObject(false) }
                }
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentPropertiesAsync(hash))
                .ReturnsAsync(properties);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentContentsAsync(hash))
                .ReturnsAsync(new[]
                {
                    new TorrentContent { Index = 0, Priority = TorrentContentPriority.Normal }
                });

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateStallRulesAsync(It.IsAny<QBitItemWrapper>()))
                .ReturnsAsync((true, DeleteReason.Stalled, true)); // Rule matched

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            Assert.True(result.ShouldRemove);
            Assert.Equal(DeleteReason.Stalled, result.DeleteReason);
            Assert.True(result.DeleteFromClient);
        }
    }

    public class ShouldRemoveFromArrQueueAsync_IntegrationScenarios : QBitServiceTests
    {
        public ShouldRemoveFromArrQueueAsync_IntegrationScenarios(QBitServiceFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task SlowCheckPasses_ButStalledCheckFails_RemovesFromQueue()
        {
            // Arrange
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            var torrentInfo = new TorrentInfo
            {
                Hash = hash,
                Name = "Test Torrent",
                State = TorrentState.StalledDownload, // Stalled, not downloading
                DownloadSpeed = 0
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentListAsync(It.Is<TorrentListQuery>(q => q.Hashes != null && q.Hashes.Contains(hash))))
                .ReturnsAsync(new[] { torrentInfo });

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentTrackersAsync(hash))
                .ReturnsAsync(Array.Empty<TorrentTracker>());

            var properties = new TorrentProperties
            {
                AdditionalData = new Dictionary<string, JToken>
                {
                    { "is_private", JToken.FromObject(false) }
                }
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentPropertiesAsync(hash))
                .ReturnsAsync(properties);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentContentsAsync(hash))
                .ReturnsAsync(new[]
                {
                    new TorrentContent { Index = 0, Priority = TorrentContentPriority.Normal }
                });

            // Slow check is skipped because not in downloading state
            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateStallRulesAsync(It.IsAny<QBitItemWrapper>()))
                .ReturnsAsync((true, DeleteReason.Stalled, true));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            Assert.True(result.ShouldRemove);
            Assert.Equal(DeleteReason.Stalled, result.DeleteReason);
            _fixture.RuleEvaluator.Verify(
                x => x.EvaluateSlowRulesAsync(It.IsAny<QBitItemWrapper>()),
                Times.Never); // Skipped
            _fixture.RuleEvaluator.Verify(
                x => x.EvaluateStallRulesAsync(It.IsAny<QBitItemWrapper>()),
                Times.Once);
        }

        [Fact]
        public async Task BothChecksPass_DoesNotRemove()
        {
            // Arrange
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            var torrentInfo = new TorrentInfo
            {
                Hash = hash,
                Name = "Test Torrent",
                State = TorrentState.Downloading,
                DownloadSpeed = 5000000 // Good speed
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentListAsync(It.Is<TorrentListQuery>(q => q.Hashes != null && q.Hashes.Contains(hash))))
                .ReturnsAsync(new[] { torrentInfo });

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentTrackersAsync(hash))
                .ReturnsAsync(Array.Empty<TorrentTracker>());

            var properties = new TorrentProperties
            {
                AdditionalData = new Dictionary<string, JToken>
                {
                    { "is_private", JToken.FromObject(false) }
                }
            };

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentPropertiesAsync(hash))
                .ReturnsAsync(properties);

            _fixture.ClientWrapper
                .Setup(x => x.GetTorrentContentsAsync(hash))
                .ReturnsAsync(new[]
                {
                    new TorrentContent { Index = 0, Priority = TorrentContentPriority.Normal }
                });

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateSlowRulesAsync(It.IsAny<QBitItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            _fixture.RuleEvaluator
                .Setup(x => x.EvaluateStallRulesAsync(It.IsAny<QBitItemWrapper>()))
                .ReturnsAsync((false, DeleteReason.None, false));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            Assert.False(result.ShouldRemove);
            Assert.Equal(DeleteReason.None, result.DeleteReason);
        }
    }
}
