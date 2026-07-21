using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.DownloadClient.QBittorrent;
using Cleanuparr.Persistence.Models.Configuration.MalwareBlocker;
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
                .Returns((false, DeleteReason.None, false, false));

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<QBitItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

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
                .Returns((false, DeleteReason.None, false, false));

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<QBitItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

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
                .Returns((false, DeleteReason.None, false, false));

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<QBitItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

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
                .Returns((false, DeleteReason.None, false, false));

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
                .Returns((false, DeleteReason.None, false, false));

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
                .Returns((true, DeleteReason.SlowSpeed, true, false)); // Rule matched

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            result.ShouldRemove.ShouldBeTrue();
            result.DeleteReason.ShouldBe(DeleteReason.SlowSpeed);
            result.DeleteFromClient.ShouldBeTrue();
            result.ChangeCategory.ShouldBeFalse();
        }

        [Fact]
        public async Task SlowDownload_RuleWithChangeCategory_PropagatesChangeCategoryFlag()
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
                .Returns((true, DeleteReason.SlowSpeed, false, true));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            result.ShouldRemove.ShouldBeTrue();
            result.DeleteReason.ShouldBe(DeleteReason.SlowSpeed);
            result.DeleteFromClient.ShouldBeFalse();
            result.ChangeCategory.ShouldBeTrue();
        }

        [Fact]
        public async Task SlowDownload_WhenAlternateSpeedActive_PassesAltSpeedActiveToEvaluator()
        {
            // Arrange
            const string hash = "test-hash";
            QBitService sut = _fixture.CreateSut();

            TorrentInfo torrentInfo = new()
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

            TorrentProperties properties = new()
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

            _fixture.ClientWrapper
                .GetAlternativeSpeedLimitsEnabledAsync()
                .Returns(true);

            _fixture.RuleEvaluator
                .EvaluateSlowRulesAsync(Arg.Any<QBitItemWrapper>(), Arg.Any<bool>())
                .Returns((false, DeleteReason.None, false, false));

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<QBitItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

            // Act
            await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            await _fixture.RuleEvaluator.Received(1)
                .EvaluateSlowRulesAsync(Arg.Any<QBitItemWrapper>(), true);
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
                .Returns((false, DeleteReason.None, false, false));

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
                .Returns((true, DeleteReason.Stalled, true, false)); // Rule matched

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            result.ShouldRemove.ShouldBeTrue();
            result.DeleteReason.ShouldBe(DeleteReason.Stalled);
            result.DeleteFromClient.ShouldBeTrue();
            result.ChangeCategory.ShouldBeFalse();
        }

        [Fact]
        public async Task StalledDownload_RuleWithChangeCategory_PropagatesChangeCategoryFlag()
        {
            // Arrange
            const string hash = "test-hash";
            var sut = _fixture.CreateSut();

            var torrentInfo = new TorrentInfo
            {
                Hash = hash,
                Name = "Test Torrent",
                State = TorrentState.StalledDownload
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
                .Returns((true, DeleteReason.Stalled, true, true));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            result.ShouldRemove.ShouldBeTrue();
            result.DeleteReason.ShouldBe(DeleteReason.Stalled);
            result.DeleteFromClient.ShouldBeTrue();
            result.ChangeCategory.ShouldBeTrue();
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
                .Returns((true, DeleteReason.Stalled, true, false));

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
                .Returns((false, DeleteReason.None, false, false));

            _fixture.RuleEvaluator
                .EvaluateStallRulesAsync(Arg.Any<QBitItemWrapper>())
                .Returns((false, DeleteReason.None, false, false));

            // Act
            var result = await sut.ShouldRemoveFromArrQueueAsync(hash, Array.Empty<string>());

            // Assert
            result.ShouldRemove.ShouldBeFalse();
            result.DeleteReason.ShouldBe(DeleteReason.None);
        }
    }

    public class BlockUnwantedFilesAsyncScenarios : QBitServiceTests
    {
        public BlockUnwantedFilesAsyncScenarios(QBitServiceFixture fixture) : base(fixture)
        {
        }

        private void SetMalwareBlockerContext(ContentBlockerConfig? config = null)
        {
            ContextProvider.Set(config ?? new ContentBlockerConfig());
            ContextProvider.Set(nameof(InstanceType), (object)InstanceType.Sonarr);

            _fixture.BlocklistProvider
                .GetBlocklistType(Arg.Any<InstanceType>())
                .Returns(BlocklistType.Blacklist);
            _fixture.BlocklistProvider
                .GetPatterns(Arg.Any<InstanceType>())
                .Returns(new ConcurrentBag<string>());
            _fixture.BlocklistProvider
                .GetRegexes(Arg.Any<InstanceType>())
                .Returns(new ConcurrentBag<Regex>());
        }

        private static TorrentInfo MakeTorrentInfo(string hash) => new()
        {
            Hash = hash,
            Name = "Malware Torrent",
            State = TorrentState.Downloading,
            DownloadSpeed = 1000,
        };

        private static TorrentProperties MakeTorrentProperties(bool isPrivate = false) => new()
        {
            AdditionalData = new Dictionary<string, JToken>
            {
                { "is_private", JToken.FromObject(isPrivate) },
            },
        };

        private void StubClient(string hash, IReadOnlyList<TorrentContent> files, bool isPrivate = false)
        {
            _fixture.ClientWrapper
                .GetTorrentListAsync(Arg.Is<TorrentListQuery>(q => q.Hashes != null && q.Hashes.Contains(hash)))
                .Returns(new[] { MakeTorrentInfo(hash) });

            _fixture.ClientWrapper
                .GetTorrentTrackersAsync(hash)
                .Returns(Array.Empty<TorrentTracker>());

            _fixture.ClientWrapper
                .GetTorrentPropertiesAsync(hash)
                .Returns(MakeTorrentProperties(isPrivate));

            _fixture.ClientWrapper
                .GetTorrentContentsAsync(hash)
                .Returns(files);
        }

        [Fact]
        public async Task AllFilesAreMalware_MarksForRemoval_WithAllFilesBlockedReason()
        {
            const string hash = "all-malware-hash";
            QBitService sut = _fixture.CreateSut();
            SetMalwareBlockerContext();

            StubClient(hash,
            [
                new TorrentContent { Name = "malware.exe", Index = 0, Priority = TorrentContentPriority.Normal },
            ]);

            _fixture.FilenameEvaluator
                .IsValid(Arg.Any<string>(), Arg.Any<BlocklistType>(), Arg.Any<ConcurrentBag<string>>(), Arg.Any<ConcurrentBag<Regex>>())
                .Returns(false);

            BlockFilesResult result = await sut.BlockUnwantedFilesAsync(hash, Array.Empty<string>());

            result.Found.ShouldBeTrue();
            result.ShouldRemove.ShouldBeTrue();
            result.DeleteReason.ShouldBe(DeleteReason.AllFilesBlocked);
        }

        [Fact]
        public async Task PartialMalware_CallsSetFilePriority_AndDoesNotMarkForRemoval()
        {
            const string hash = "partial-malware-hash";
            QBitService sut = _fixture.CreateSut();
            SetMalwareBlockerContext();

            StubClient(hash,
            [
                new TorrentContent { Name = "movie.mkv", Index = 0, Priority = TorrentContentPriority.Normal },
                new TorrentContent { Name = "installer.exe", Index = 1, Priority = TorrentContentPriority.Normal },
            ]);

            _fixture.FilenameEvaluator
                .IsValid(Arg.Is<string>(name => name.EndsWith("installer.exe")), Arg.Any<BlocklistType>(), Arg.Any<ConcurrentBag<string>>(), Arg.Any<ConcurrentBag<Regex>>())
                .Returns(false);
            _fixture.FilenameEvaluator
                .IsValid(Arg.Is<string>(name => name.EndsWith("movie.mkv")), Arg.Any<BlocklistType>(), Arg.Any<ConcurrentBag<string>>(), Arg.Any<ConcurrentBag<Regex>>())
                .Returns(true);

            BlockFilesResult result = await sut.BlockUnwantedFilesAsync(hash, Array.Empty<string>());

            result.Found.ShouldBeTrue();
            result.ShouldRemove.ShouldBeFalse();
            result.DeleteReason.ShouldBe(DeleteReason.None);

            await _fixture.ClientWrapper
                .Received(1)
                .SetFilePriorityAsync(hash, 1, TorrentContentPriority.Skip);
        }

        [Fact]
        public async Task PartialMalware_WithDeleteIfAnyFileBlocked_MarksForRemoval_AndSkipsSetFilePriority()
        {
            const string hash = "partial-malware-any-hash";
            QBitService sut = _fixture.CreateSut();
            SetMalwareBlockerContext(new ContentBlockerConfig { DeleteIfAnyFileBlocked = true });

            StubClient(hash,
            [
                new TorrentContent { Name = "movie.mkv", Index = 0, Priority = TorrentContentPriority.Normal },
                new TorrentContent { Name = "installer.exe", Index = 1, Priority = TorrentContentPriority.Normal },
            ]);

            _fixture.FilenameEvaluator
                .IsValid(Arg.Is<string>(name => name.EndsWith("installer.exe")), Arg.Any<BlocklistType>(), Arg.Any<ConcurrentBag<string>>(), Arg.Any<ConcurrentBag<Regex>>())
                .Returns(false);
            _fixture.FilenameEvaluator
                .IsValid(Arg.Is<string>(name => name.EndsWith("movie.mkv")), Arg.Any<BlocklistType>(), Arg.Any<ConcurrentBag<string>>(), Arg.Any<ConcurrentBag<Regex>>())
                .Returns(true);

            BlockFilesResult result = await sut.BlockUnwantedFilesAsync(hash, Array.Empty<string>());

            result.Found.ShouldBeTrue();
            result.ShouldRemove.ShouldBeTrue();
            result.DeleteReason.ShouldBe(DeleteReason.AtLeastOneFileBlocked);

            await _fixture.ClientWrapper
                .DidNotReceive()
                .SetFilePriorityAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TorrentContentPriority>());
        }

        [Fact]
        public async Task NoUnwantedFiles_DoesNotMarkForRemoval()
        {
            const string hash = "clean-hash";
            QBitService sut = _fixture.CreateSut();
            SetMalwareBlockerContext();

            StubClient(hash,
            [
                new TorrentContent { Name = "movie.mkv", Index = 0, Priority = TorrentContentPriority.Normal },
            ]);

            _fixture.FilenameEvaluator
                .IsValid(Arg.Any<string>(), Arg.Any<BlocklistType>(), Arg.Any<ConcurrentBag<string>>(), Arg.Any<ConcurrentBag<Regex>>())
                .Returns(true);

            BlockFilesResult result = await sut.BlockUnwantedFilesAsync(hash, Array.Empty<string>());

            result.Found.ShouldBeTrue();
            result.ShouldRemove.ShouldBeFalse();
            result.DeleteReason.ShouldBe(DeleteReason.None);

            await _fixture.ClientWrapper
                .DidNotReceive()
                .SetFilePriorityAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TorrentContentPriority>());
        }

        [Fact]
        public async Task AlreadySkippedFile_DoesNotTriggerEarlyReturn_WhenDeleteIfAnyFileBlocked()
        {
            const string hash = "already-skipped-hash";
            QBitService sut = _fixture.CreateSut();
            SetMalwareBlockerContext(new ContentBlockerConfig { DeleteIfAnyFileBlocked = true });

            StubClient(hash,
            [
                new TorrentContent { Name = "movie.mkv", Index = 0, Priority = TorrentContentPriority.Normal },
                new TorrentContent { Name = "installer.exe", Index = 1, Priority = TorrentContentPriority.Skip },
            ]);

            _fixture.FilenameEvaluator
                .IsValid(Arg.Is<string>(name => name.EndsWith("movie.mkv")), Arg.Any<BlocklistType>(), Arg.Any<ConcurrentBag<string>>(), Arg.Any<ConcurrentBag<Regex>>())
                .Returns(true);

            BlockFilesResult result = await sut.BlockUnwantedFilesAsync(hash, Array.Empty<string>());

            result.Found.ShouldBeTrue();
            result.ShouldRemove.ShouldBeFalse();
            result.DeleteReason.ShouldBe(DeleteReason.None);
        }

        [Fact]
        public async Task AllFilesAlreadySkipped_NoNewMalware_DoesNotMarkForRemoval()
        {
            const string hash = "all-skipped-hash";
            QBitService sut = _fixture.CreateSut();
            SetMalwareBlockerContext();

            StubClient(hash,
            [
                new TorrentContent { Name = "movie.mkv", Index = 0, Priority = TorrentContentPriority.Skip },
                new TorrentContent { Name = "installer.exe", Index = 1, Priority = TorrentContentPriority.Skip },
            ]);

            BlockFilesResult result = await sut.BlockUnwantedFilesAsync(hash, Array.Empty<string>());

            result.Found.ShouldBeTrue();
            result.ShouldRemove.ShouldBeFalse();
            result.DeleteReason.ShouldBe(DeleteReason.None);
        }
    }
}
