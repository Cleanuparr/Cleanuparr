using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.DownloadClient.QBittorrent;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using NSubstitute;
using Newtonsoft.Json.Linq;
using QBittorrent.Client;
using Shouldly;
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
                .GetTorrentListAsync(Arg.Is<TorrentListQuery>(q => q.Hashes != null && q.Hashes.Contains(hash)))
                .Returns(Array.Empty<TorrentInfo>());

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            result.Found.ShouldBeFalse();
            result.ShouldRemove.ShouldBeFalse();
            result.DeleteReason.ShouldBe(DeleteReason.None);
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
                .GetTorrentListAsync(Arg.Is<TorrentListQuery>(q => q.Hashes != null && q.Hashes.Contains(hash)))
                .Returns(new[] { torrentInfo });

            _fixture.ClientWrapper
                .GetTorrentTrackersAsync(hash)
                .Returns(Array.Empty<TorrentTracker>());

            var properties = new TorrentProperties
            {
                AdditionalData = new Dictionary<string, JToken>
                {
                    { "is_private", JToken.FromObject(false) }
                }
            };

            _fixture.ClientWrapper
                .GetTorrentPropertiesAsync(hash)
                .Returns(properties);

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, new[] { ignoredCategory });

            // Assert
            result.Found.ShouldBeTrue();
            result.ShouldRemove.ShouldBeFalse();
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
                .GetTorrentListAsync(Arg.Is<TorrentListQuery>(q => q.Hashes != null && q.Hashes.Contains(hash)))
                .Returns(new[] { torrentInfo });

            _fixture.ClientWrapper
                .GetTorrentTrackersAsync(hash)
                .Returns(Array.Empty<TorrentTracker>());

            var properties = new TorrentProperties
            {
                AdditionalData = new Dictionary<string, JToken>
                {
                    { "is_private", JToken.FromObject(true) }
                }
            };

            _fixture.ClientWrapper
                .GetTorrentPropertiesAsync(hash)
                .Returns(properties);

            _fixture.ClientWrapper
                .GetTorrentContentsAsync(hash)
                .Returns(new[]
                {
                    new TorrentContent { Index = 0, Priority = TorrentContentPriority.Normal }
                });

            _fixture.RuleEvaluator
                .EvaluateSlowRulesAsync(Arg.Any<QBitItemWrapper>())
                .Returns((false, DeleteReason.None, false));

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<QBitItemWrapper>())
                .Returns((false, DeleteReason.None, false));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            result.Found.ShouldBeTrue();
            result.IsPrivate.ShouldBeTrue();
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
                .GetTorrentListAsync(Arg.Is<TorrentListQuery>(q => q.Hashes != null && q.Hashes.Contains(hash)))
                .Returns(new[] { torrentInfo });

            _fixture.ClientWrapper
                .GetTorrentTrackersAsync(hash)
                .Returns(Array.Empty<TorrentTracker>());

            var properties = new TorrentProperties
            {
                AdditionalData = new Dictionary<string, JToken>
                {
                    { "is_private", JToken.FromObject(false) }
                }
            };

            _fixture.ClientWrapper
                .GetTorrentPropertiesAsync(hash)
                .Returns(properties);

            _fixture.ClientWrapper
                .GetTorrentContentsAsync(hash)
                .Returns(new[]
                {
                    new TorrentContent { Index = 0, Priority = TorrentContentPriority.Normal }
                });

            _fixture.RuleEvaluator
                .EvaluateSlowRulesAsync(Arg.Any<QBitItemWrapper>())
                .Returns((false, DeleteReason.None, false));

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<QBitItemWrapper>())
                .Returns((false, DeleteReason.None, false));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            result.Found.ShouldBeTrue();
            result.IsPrivate.ShouldBeFalse();
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
                .GetTorrentListAsync(Arg.Is<TorrentListQuery>(q => q.Hashes != null && q.Hashes.Contains(hash)))
                .Returns(new[] { torrentInfo });

            _fixture.ClientWrapper
                .GetTorrentTrackersAsync(hash)
                .Returns(Array.Empty<TorrentTracker>());

            _fixture.ClientWrapper
                .GetTorrentPropertiesAsync(hash)
                .Returns((TorrentProperties?)null); // Properties not found

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            result.Found.ShouldBeFalse();
            result.ShouldRemove.ShouldBeFalse();
            result.IsPrivate.ShouldBeFalse();
            result.DeleteReason.ShouldBe(DeleteReason.None);
            result.DeleteFromClient.ShouldBeFalse();
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
                .GetTorrentListAsync(Arg.Is<TorrentListQuery>(q => q.Hashes != null && q.Hashes.Contains(hash)))
                .Returns(new[] { torrentInfo });

            _fixture.ClientWrapper
                .GetTorrentTrackersAsync(hash)
                .Returns(Array.Empty<TorrentTracker>());

            var properties = new TorrentProperties
            {
                AdditionalData = new Dictionary<string, JToken>
                {
                    { "is_private", JToken.FromObject(false) }
                }
            };

            _fixture.ClientWrapper
                .GetTorrentPropertiesAsync(hash)
                .Returns(properties);

            _fixture.ClientWrapper
                .GetTorrentContentsAsync(hash)
                .Returns(new[]
                {
                    new TorrentContent { Index = 0, Priority = TorrentContentPriority.Skip },
                    new TorrentContent { Index = 1, Priority = TorrentContentPriority.Skip }
                });

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            result.ShouldRemove.ShouldBeTrue();
            result.DeleteReason.ShouldBe(DeleteReason.AllFilesSkippedByQBit);
            result.DeleteFromClient.ShouldBeTrue();
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
                .GetTorrentListAsync(Arg.Is<TorrentListQuery>(q => q.Hashes != null && q.Hashes.Contains(hash)))
                .Returns(new[] { torrentInfo });

            _fixture.ClientWrapper
                .GetTorrentTrackersAsync(hash)
                .Returns(Array.Empty<TorrentTracker>());

            var properties = new TorrentProperties
            {
                AdditionalData = new Dictionary<string, JToken>
                {
                    { "is_private", JToken.FromObject(false) }
                }
            };

            _fixture.ClientWrapper
                .GetTorrentPropertiesAsync(hash)
                .Returns(properties);

            _fixture.ClientWrapper
                .GetTorrentContentsAsync(hash)
                .Returns(new[]
                {
                    new TorrentContent { Index = 0, Priority = TorrentContentPriority.Skip },
                    new TorrentContent { Index = 1, Priority = TorrentContentPriority.Skip }
                });

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            result.ShouldRemove.ShouldBeTrue();
            result.DeleteReason.ShouldBe(DeleteReason.AllFilesSkipped);
            result.DeleteFromClient.ShouldBeTrue();
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
                .GetTorrentListAsync(Arg.Is<TorrentListQuery>(q => q.Hashes != null && q.Hashes.Contains(hash)))
                .Returns(new[] { torrentInfo });

            _fixture.ClientWrapper
                .GetTorrentTrackersAsync(hash)
                .Returns(Array.Empty<TorrentTracker>());

            var properties = new TorrentProperties
            {
                AdditionalData = new Dictionary<string, JToken>
                {
                    { "is_private", JToken.FromObject(false) }
                }
            };

            _fixture.ClientWrapper
                .GetTorrentPropertiesAsync(hash)
                .Returns(properties);

            _fixture.ClientWrapper
                .GetTorrentContentsAsync(hash)
                .Returns(new[]
                {
                    new TorrentContent { Index = 0, Priority = TorrentContentPriority.Skip },
                    new TorrentContent { Index = 1, Priority = TorrentContentPriority.Normal } // At least one wanted
                });

            _fixture.RuleEvaluator
                .EvaluateSlowRulesAsync(Arg.Any<QBitItemWrapper>())
                .Returns((false, DeleteReason.None, false));

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<QBitItemWrapper>())
                .Returns((false, DeleteReason.None, false));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            result.ShouldRemove.ShouldBeFalse();
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
                .GetTorrentListAsync(Arg.Is<TorrentListQuery>(q => q.Hashes != null && q.Hashes.Contains(hash)))
                .Returns(new[] { torrentInfo });

            _fixture.ClientWrapper
                .GetTorrentTrackersAsync(hash)
                .Returns(Array.Empty<TorrentTracker>());

            var properties = new TorrentProperties
            {
                AdditionalData = new Dictionary<string, JToken>
                {
                    { "is_private", JToken.FromObject(false) }
                }
            };

            _fixture.ClientWrapper
                .GetTorrentPropertiesAsync(hash)
                .Returns(properties);

            _fixture.ClientWrapper
                .GetTorrentContentsAsync(hash)
                .Returns(new[]
                {
                    new TorrentContent { Index = 0, Priority = TorrentContentPriority.Normal }
                });

            _fixture.Striker
                .StrikeAndCheckLimit(hash, Arg.Any<string>(), (ushort)3, StrikeType.DownloadingMetadata, Arg.Any<long?>())
                .Returns(false);

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            result.ShouldRemove.ShouldBeFalse();
            await _fixture.Striker.Received(1)
                .StrikeAndCheckLimit(hash, Arg.Any<string>(), (ushort)3, StrikeType.DownloadingMetadata, Arg.Any<long?>());
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
                .GetTorrentListAsync(Arg.Is<TorrentListQuery>(q => q.Hashes != null && q.Hashes.Contains(hash)))
                .Returns(new[] { torrentInfo });

            _fixture.ClientWrapper
                .GetTorrentTrackersAsync(hash)
                .Returns(Array.Empty<TorrentTracker>());

            var properties = new TorrentProperties
            {
                AdditionalData = new Dictionary<string, JToken>
                {
                    { "is_private", JToken.FromObject(false) }
                }
            };

            _fixture.ClientWrapper
                .GetTorrentPropertiesAsync(hash)
                .Returns(properties);

            _fixture.ClientWrapper
                .GetTorrentContentsAsync(hash)
                .Returns(new[]
                {
                    new TorrentContent { Index = 0, Priority = TorrentContentPriority.Normal }
                });

            _fixture.Striker
                .StrikeAndCheckLimit(hash, Arg.Any<string>(), (ushort)3, StrikeType.DownloadingMetadata, Arg.Any<long?>())
                .Returns(true); // Strike limit exceeded

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            result.ShouldRemove.ShouldBeTrue();
            result.DeleteReason.ShouldBe(DeleteReason.DownloadingMetadata);
            result.DeleteFromClient.ShouldBeTrue();
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
                .GetTorrentListAsync(Arg.Is<TorrentListQuery>(q => q.Hashes != null && q.Hashes.Contains(hash)))
                .Returns(new[] { torrentInfo });

            _fixture.ClientWrapper
                .GetTorrentTrackersAsync(hash)
                .Returns(Array.Empty<TorrentTracker>());

            var properties = new TorrentProperties
            {
                AdditionalData = new Dictionary<string, JToken>
                {
                    { "is_private", JToken.FromObject(false) }
                }
            };

            _fixture.ClientWrapper
                .GetTorrentPropertiesAsync(hash)
                .Returns(properties);

            _fixture.ClientWrapper
                .GetTorrentContentsAsync(hash)
                .Returns(new[]
                {
                    new TorrentContent { Index = 0, Priority = TorrentContentPriority.Normal }
                });

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            result.ShouldRemove.ShouldBeFalse();
            await _fixture.Striker.DidNotReceive()
                .StrikeAndCheckLimit(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ushort>(), Arg.Any<StrikeType>(), Arg.Any<long?>());
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
                .GetTorrentListAsync(Arg.Is<TorrentListQuery>(q => q.Hashes != null && q.Hashes.Contains(hash)))
                .Returns(new[] { torrentInfo });

            _fixture.ClientWrapper
                .GetTorrentTrackersAsync(hash)
                .Returns(Array.Empty<TorrentTracker>());

            var properties = new TorrentProperties
            {
                AdditionalData = new Dictionary<string, JToken>
                {
                    { "is_private", JToken.FromObject(false) }
                }
            };

            _fixture.ClientWrapper
                .GetTorrentPropertiesAsync(hash)
                .Returns(properties);

            _fixture.ClientWrapper
                .GetTorrentContentsAsync(hash)
                .Returns(new[]
                {
                    new TorrentContent { Index = 0, Priority = TorrentContentPriority.Normal }
                });

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<QBitItemWrapper>())
                .Returns((false, DeleteReason.None, false));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            result.ShouldRemove.ShouldBeFalse();
            await _fixture.RuleEvaluator.DidNotReceive()
                .EvaluateSlowRulesAsync(Arg.Any<QBitItemWrapper>());
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
                .GetTorrentListAsync(Arg.Is<TorrentListQuery>(q => q.Hashes != null && q.Hashes.Contains(hash)))
                .Returns(new[] { torrentInfo });

            _fixture.ClientWrapper
                .GetTorrentTrackersAsync(hash)
                .Returns(Array.Empty<TorrentTracker>());

            var properties = new TorrentProperties
            {
                AdditionalData = new Dictionary<string, JToken>
                {
                    { "is_private", JToken.FromObject(false) }
                }
            };

            _fixture.ClientWrapper
                .GetTorrentPropertiesAsync(hash)
                .Returns(properties);

            _fixture.ClientWrapper
                .GetTorrentContentsAsync(hash)
                .Returns(new[]
                {
                    new TorrentContent { Index = 0, Priority = TorrentContentPriority.Normal }
                });

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<QBitItemWrapper>())
                .Returns((false, DeleteReason.None, false));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            result.ShouldRemove.ShouldBeFalse();
            await _fixture.RuleEvaluator.DidNotReceive()
                .EvaluateSlowRulesAsync(Arg.Any<QBitItemWrapper>());
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
                .GetTorrentListAsync(Arg.Is<TorrentListQuery>(q => q.Hashes != null && q.Hashes.Contains(hash)))
                .Returns(new[] { torrentInfo });

            _fixture.ClientWrapper
                .GetTorrentTrackersAsync(hash)
                .Returns(Array.Empty<TorrentTracker>());

            var properties = new TorrentProperties
            {
                AdditionalData = new Dictionary<string, JToken>
                {
                    { "is_private", JToken.FromObject(false) }
                }
            };

            _fixture.ClientWrapper
                .GetTorrentPropertiesAsync(hash)
                .Returns(properties);

            _fixture.ClientWrapper
                .GetTorrentContentsAsync(hash)
                .Returns(new[]
                {
                    new TorrentContent { Index = 0, Priority = TorrentContentPriority.Normal }
                });

            _fixture.RuleEvaluator
                .EvaluateSlowRulesAsync(Arg.Any<QBitItemWrapper>())
                .Returns((true, DeleteReason.SlowSpeed, true)); // Rule matched

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            result.ShouldRemove.ShouldBeTrue();
            result.DeleteReason.ShouldBe(DeleteReason.SlowSpeed);
            result.DeleteFromClient.ShouldBeTrue();
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
                .GetTorrentListAsync(Arg.Is<TorrentListQuery>(q => q.Hashes != null && q.Hashes.Contains(hash)))
                .Returns(new[] { torrentInfo });

            _fixture.ClientWrapper
                .GetTorrentTrackersAsync(hash)
                .Returns(Array.Empty<TorrentTracker>());

            var properties = new TorrentProperties
            {
                AdditionalData = new Dictionary<string, JToken>
                {
                    { "is_private", JToken.FromObject(false) }
                }
            };

            _fixture.ClientWrapper
                .GetTorrentPropertiesAsync(hash)
                .Returns(properties);

            _fixture.ClientWrapper
                .GetTorrentContentsAsync(hash)
                .Returns(new[]
                {
                    new TorrentContent { Index = 0, Priority = TorrentContentPriority.Normal }
                });

            _fixture.RuleEvaluator
                .EvaluateSlowRulesAsync(Arg.Any<QBitItemWrapper>())
                .Returns((false, DeleteReason.None, false));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            result.ShouldRemove.ShouldBeFalse();
            await _fixture.RuleEvaluator.DidNotReceive()
                .EvaluateStallRulesAsync(Arg.Any<QBitItemWrapper>());
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
                .GetTorrentListAsync(Arg.Is<TorrentListQuery>(q => q.Hashes != null && q.Hashes.Contains(hash)))
                .Returns(new[] { torrentInfo });

            _fixture.ClientWrapper
                .GetTorrentTrackersAsync(hash)
                .Returns(Array.Empty<TorrentTracker>());

            var properties = new TorrentProperties
            {
                AdditionalData = new Dictionary<string, JToken>
                {
                    { "is_private", JToken.FromObject(false) }
                }
            };

            _fixture.ClientWrapper
                .GetTorrentPropertiesAsync(hash)
                .Returns(properties);

            _fixture.ClientWrapper
                .GetTorrentContentsAsync(hash)
                .Returns(new[]
                {
                    new TorrentContent { Index = 0, Priority = TorrentContentPriority.Normal }
                });

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<QBitItemWrapper>())
                .Returns((true, DeleteReason.Stalled, true)); // Rule matched

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            result.ShouldRemove.ShouldBeTrue();
            result.DeleteReason.ShouldBe(DeleteReason.Stalled);
            result.DeleteFromClient.ShouldBeTrue();
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
                .GetTorrentListAsync(Arg.Is<TorrentListQuery>(q => q.Hashes != null && q.Hashes.Contains(hash)))
                .Returns(new[] { torrentInfo });

            _fixture.ClientWrapper
                .GetTorrentTrackersAsync(hash)
                .Returns(Array.Empty<TorrentTracker>());

            var properties = new TorrentProperties
            {
                AdditionalData = new Dictionary<string, JToken>
                {
                    { "is_private", JToken.FromObject(false) }
                }
            };

            _fixture.ClientWrapper
                .GetTorrentPropertiesAsync(hash)
                .Returns(properties);

            _fixture.ClientWrapper
                .GetTorrentContentsAsync(hash)
                .Returns(new[]
                {
                    new TorrentContent { Index = 0, Priority = TorrentContentPriority.Normal }
                });

            // Slow check is skipped because not in downloading state
            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<QBitItemWrapper>())
                .Returns((true, DeleteReason.Stalled, true));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            result.ShouldRemove.ShouldBeTrue();
            result.DeleteReason.ShouldBe(DeleteReason.Stalled);
            await _fixture.RuleEvaluator.DidNotReceive()
                .EvaluateSlowRulesAsync(Arg.Any<QBitItemWrapper>()); // Skipped
            await _fixture.RuleEvaluator.Received(1)
                .EvaluateStallRulesAsync(Arg.Any<QBitItemWrapper>());
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
                .GetTorrentListAsync(Arg.Is<TorrentListQuery>(q => q.Hashes != null && q.Hashes.Contains(hash)))
                .Returns(new[] { torrentInfo });

            _fixture.ClientWrapper
                .GetTorrentTrackersAsync(hash)
                .Returns(Array.Empty<TorrentTracker>());

            var properties = new TorrentProperties
            {
                AdditionalData = new Dictionary<string, JToken>
                {
                    { "is_private", JToken.FromObject(false) }
                }
            };

            _fixture.ClientWrapper
                .GetTorrentPropertiesAsync(hash)
                .Returns(properties);

            _fixture.ClientWrapper
                .GetTorrentContentsAsync(hash)
                .Returns(new[]
                {
                    new TorrentContent { Index = 0, Priority = TorrentContentPriority.Normal }
                });

            _fixture.RuleEvaluator
                .EvaluateSlowRulesAsync(Arg.Any<QBitItemWrapper>())
                .Returns((false, DeleteReason.None, false));

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<QBitItemWrapper>())
                .Returns((false, DeleteReason.None, false));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            result.ShouldRemove.ShouldBeFalse();
            result.DeleteReason.ShouldBe(DeleteReason.None);
        }
    }
}
