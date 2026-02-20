using Cleanuparr.Domain.Entities.RTorrent.Response;
using Cleanuparr.Infrastructure.Features.DownloadClient.RTorrent;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient;

public class RTorrentItemWrapperTests
{
    public class PropertyMapping_Tests
    {
        [Fact]
        public void MapsHash()
        {
            // Arrange
            var torrent = new RTorrentTorrent { Hash = "ABC123DEF456", Name = "Test" };

            // Act
            var wrapper = new RTorrentItemWrapper(torrent);

            // Assert
            Assert.Equal("ABC123DEF456", wrapper.Hash);
        }

        [Fact]
        public void MapsName()
        {
            // Arrange
            var torrent = new RTorrentTorrent { Hash = "HASH1", Name = "Test Torrent Name" };

            // Act
            var wrapper = new RTorrentItemWrapper(torrent);

            // Assert
            Assert.Equal("Test Torrent Name", wrapper.Name);
        }

        [Fact]
        public void MapsIsPrivate_True()
        {
            // Arrange
            var torrent = new RTorrentTorrent { Hash = "HASH1", Name = "Test", IsPrivate = 1 };

            // Act
            var wrapper = new RTorrentItemWrapper(torrent);

            // Assert
            Assert.True(wrapper.IsPrivate);
        }

        [Fact]
        public void MapsIsPrivate_False()
        {
            // Arrange
            var torrent = new RTorrentTorrent { Hash = "HASH1", Name = "Test", IsPrivate = 0 };

            // Act
            var wrapper = new RTorrentItemWrapper(torrent);

            // Assert
            Assert.False(wrapper.IsPrivate);
        }

        [Fact]
        public void MapsSize()
        {
            // Arrange
            var torrent = new RTorrentTorrent { Hash = "HASH1", Name = "Test", SizeBytes = 1024000 };

            // Act
            var wrapper = new RTorrentItemWrapper(torrent);

            // Assert
            Assert.Equal(1024000, wrapper.Size);
        }

        [Fact]
        public void MapsDownloadSpeed()
        {
            // Arrange
            var torrent = new RTorrentTorrent { Hash = "HASH1", Name = "Test", DownRate = 500000 };

            // Act
            var wrapper = new RTorrentItemWrapper(torrent);

            // Assert
            Assert.Equal(500000, wrapper.DownloadSpeed);
        }

        [Fact]
        public void MapsDownloadedBytes()
        {
            // Arrange
            var torrent = new RTorrentTorrent { Hash = "HASH1", Name = "Test", CompletedBytes = 750000 };

            // Act
            var wrapper = new RTorrentItemWrapper(torrent);

            // Assert
            Assert.Equal(750000, wrapper.DownloadedBytes);
        }

        [Fact]
        public void MapsCategory()
        {
            // Arrange
            var torrent = new RTorrentTorrent { Hash = "HASH1", Name = "Test", Label = "movies" };

            // Act
            var wrapper = new RTorrentItemWrapper(torrent);

            // Assert
            Assert.Equal("movies", wrapper.Category);
        }

        [Fact]
        public void CategoryIsSettable()
        {
            // Arrange
            var torrent = new RTorrentTorrent { Hash = "HASH1", Name = "Test", Label = "movies" };
            var wrapper = new RTorrentItemWrapper(torrent);

            // Act
            wrapper.Category = "tv";

            // Assert
            Assert.Equal("tv", wrapper.Category);
        }
    }

    public class Ratio_Tests
    {
        [Fact]
        public void ConvertsRatioFromRTorrentFormat()
        {
            // rTorrent returns ratio * 1000, so 1500 = 1.5 ratio
            // Arrange
            var torrent = new RTorrentTorrent { Hash = "HASH1", Name = "Test", Ratio = 1500 };

            // Act
            var wrapper = new RTorrentItemWrapper(torrent);

            // Assert
            Assert.Equal(1.5, wrapper.Ratio);
        }

        [Fact]
        public void HandlesZeroRatio()
        {
            // Arrange
            var torrent = new RTorrentTorrent { Hash = "HASH1", Name = "Test", Ratio = 0 };

            // Act
            var wrapper = new RTorrentItemWrapper(torrent);

            // Assert
            Assert.Equal(0, wrapper.Ratio);
        }

        [Fact]
        public void HandlesHighRatio()
        {
            // Arrange - 10.0 ratio = 10000 in rTorrent
            var torrent = new RTorrentTorrent { Hash = "HASH1", Name = "Test", Ratio = 10000 };

            // Act
            var wrapper = new RTorrentItemWrapper(torrent);

            // Assert
            Assert.Equal(10.0, wrapper.Ratio);
        }
    }

    public class CompletionPercentage_Tests
    {
        [Fact]
        public void CalculatesCorrectPercentage()
        {
            // Arrange
            var torrent = new RTorrentTorrent
            {
                Hash = "HASH1",
                Name = "Test",
                SizeBytes = 1000,
                CompletedBytes = 500
            };

            // Act
            var wrapper = new RTorrentItemWrapper(torrent);

            // Assert
            Assert.Equal(50.0, wrapper.CompletionPercentage);
        }

        [Fact]
        public void ReturnsZero_WhenSizeIsZero()
        {
            // Arrange
            var torrent = new RTorrentTorrent
            {
                Hash = "HASH1",
                Name = "Test",
                SizeBytes = 0,
                CompletedBytes = 0
            };

            // Act
            var wrapper = new RTorrentItemWrapper(torrent);

            // Assert
            Assert.Equal(0.0, wrapper.CompletionPercentage);
        }

        [Fact]
        public void ReturnsHundred_WhenComplete()
        {
            // Arrange
            var torrent = new RTorrentTorrent
            {
                Hash = "HASH1",
                Name = "Test",
                SizeBytes = 1000,
                CompletedBytes = 1000
            };

            // Act
            var wrapper = new RTorrentItemWrapper(torrent);

            // Assert
            Assert.Equal(100.0, wrapper.CompletionPercentage);
        }
    }

    public class IsDownloading_Tests
    {
        [Fact]
        public void ReturnsTrue_WhenStateIsStartedAndNotComplete()
        {
            // Arrange
            var torrent = new RTorrentTorrent
            {
                Hash = "HASH1",
                Name = "Test",
                State = 1,  // Started
                Complete = 0  // Not complete
            };

            // Act
            var wrapper = new RTorrentItemWrapper(torrent);

            // Assert
            Assert.True(wrapper.IsDownloading());
        }

        [Fact]
        public void ReturnsFalse_WhenStopped()
        {
            // Arrange
            var torrent = new RTorrentTorrent
            {
                Hash = "HASH1",
                Name = "Test",
                State = 0,  // Stopped
                Complete = 0
            };

            // Act
            var wrapper = new RTorrentItemWrapper(torrent);

            // Assert
            Assert.False(wrapper.IsDownloading());
        }

        [Fact]
        public void ReturnsFalse_WhenComplete()
        {
            // Arrange
            var torrent = new RTorrentTorrent
            {
                Hash = "HASH1",
                Name = "Test",
                State = 1,  // Started
                Complete = 1  // Complete (seeding)
            };

            // Act
            var wrapper = new RTorrentItemWrapper(torrent);

            // Assert
            Assert.False(wrapper.IsDownloading());
        }
    }

    public class IsStalled_Tests
    {
        [Fact]
        public void ReturnsTrue_WhenDownloadingWithNoSpeed()
        {
            // Arrange
            var torrent = new RTorrentTorrent
            {
                Hash = "HASH1",
                Name = "Test",
                State = 1,
                Complete = 0,
                DownRate = 0,
                SizeBytes = 1000,
                CompletedBytes = 500
            };

            // Act
            var wrapper = new RTorrentItemWrapper(torrent);

            // Assert
            Assert.True(wrapper.IsStalled());
        }

        [Fact]
        public void ReturnsFalse_WhenDownloadingWithSpeed()
        {
            // Arrange
            var torrent = new RTorrentTorrent
            {
                Hash = "HASH1",
                Name = "Test",
                State = 1,
                Complete = 0,
                DownRate = 100000,
                SizeBytes = 1000,
                CompletedBytes = 500
            };

            // Act
            var wrapper = new RTorrentItemWrapper(torrent);

            // Assert
            Assert.False(wrapper.IsStalled());
        }

        [Fact]
        public void ReturnsFalse_WhenNotDownloading()
        {
            // Arrange
            var torrent = new RTorrentTorrent
            {
                Hash = "HASH1",
                Name = "Test",
                State = 0,  // Stopped
                Complete = 0,
                DownRate = 0
            };

            // Act
            var wrapper = new RTorrentItemWrapper(torrent);

            // Assert
            Assert.False(wrapper.IsStalled());
        }
    }

    public class SeedingTime_Tests
    {
        [Fact]
        public void ReturnsZero_WhenNotComplete()
        {
            // Arrange
            var torrent = new RTorrentTorrent
            {
                Hash = "HASH1",
                Name = "Test",
                Complete = 0,
                TimestampFinished = 0
            };

            // Act
            var wrapper = new RTorrentItemWrapper(torrent);

            // Assert
            Assert.Equal(0, wrapper.SeedingTimeSeconds);
        }

        [Fact]
        public void ReturnsZero_WhenNoFinishTimestamp()
        {
            // Arrange
            var torrent = new RTorrentTorrent
            {
                Hash = "HASH1",
                Name = "Test",
                Complete = 1,
                TimestampFinished = 0
            };

            // Act
            var wrapper = new RTorrentItemWrapper(torrent);

            // Assert
            Assert.Equal(0, wrapper.SeedingTimeSeconds);
        }

        [Fact]
        public void CalculatesSeedingTime_WhenComplete()
        {
            // Arrange
            var finishedTime = DateTimeOffset.UtcNow.AddHours(-2).ToUnixTimeSeconds();
            var torrent = new RTorrentTorrent
            {
                Hash = "HASH1",
                Name = "Test",
                Complete = 1,
                TimestampFinished = finishedTime
            };

            // Act
            var wrapper = new RTorrentItemWrapper(torrent);

            // Assert - should be approximately 2 hours (7200 seconds)
            Assert.True(wrapper.SeedingTimeSeconds >= 7190 && wrapper.SeedingTimeSeconds <= 7210);
        }
    }

    public class Eta_Tests
    {
        [Fact]
        public void ReturnsZero_WhenNoDownloadSpeed()
        {
            // Arrange
            var torrent = new RTorrentTorrent
            {
                Hash = "HASH1",
                Name = "Test",
                SizeBytes = 1000,
                CompletedBytes = 500,
                DownRate = 0
            };

            // Act
            var wrapper = new RTorrentItemWrapper(torrent);

            // Assert
            Assert.Equal(0, wrapper.Eta);
        }

        [Fact]
        public void CalculatesEta_WhenDownloading()
        {
            // Arrange - 500 bytes remaining at 100 bytes/sec = 5 seconds ETA
            var torrent = new RTorrentTorrent
            {
                Hash = "HASH1",
                Name = "Test",
                SizeBytes = 1000,
                CompletedBytes = 500,
                DownRate = 100
            };

            // Act
            var wrapper = new RTorrentItemWrapper(torrent);

            // Assert
            Assert.Equal(5, wrapper.Eta);
        }

        [Fact]
        public void ReturnsZero_WhenComplete()
        {
            // Arrange
            var torrent = new RTorrentTorrent
            {
                Hash = "HASH1",
                Name = "Test",
                SizeBytes = 1000,
                CompletedBytes = 1000,
                DownRate = 100
            };

            // Act
            var wrapper = new RTorrentItemWrapper(torrent);

            // Assert
            Assert.Equal(0, wrapper.Eta);
        }
    }

    public class IsIgnored_Tests
    {
        [Fact]
        public void ReturnsFalse_WhenEmptyIgnoreList()
        {
            // Arrange
            var torrent = new RTorrentTorrent { Hash = "HASH1", Name = "Test", Label = "movies" };
            var wrapper = new RTorrentItemWrapper(torrent);

            // Act
            var result = wrapper.IsIgnored(new List<string>());

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ReturnsTrue_WhenHashMatches()
        {
            // Arrange
            var torrent = new RTorrentTorrent { Hash = "ABC123", Name = "Test", Label = "movies" };
            var wrapper = new RTorrentItemWrapper(torrent);

            // Act
            var result = wrapper.IsIgnored(new List<string> { "ABC123" });

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ReturnsTrue_WhenHashMatchesCaseInsensitive()
        {
            // Arrange
            var torrent = new RTorrentTorrent { Hash = "ABC123", Name = "Test", Label = "movies" };
            var wrapper = new RTorrentItemWrapper(torrent);

            // Act
            var result = wrapper.IsIgnored(new List<string> { "abc123" });

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ReturnsTrue_WhenCategoryMatches()
        {
            // Arrange
            var torrent = new RTorrentTorrent { Hash = "HASH1", Name = "Test", Label = "movies" };
            var wrapper = new RTorrentItemWrapper(torrent);

            // Act
            var result = wrapper.IsIgnored(new List<string> { "movies" });

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ReturnsTrue_WhenTrackerDomainMatches()
        {
            // Arrange
            var torrent = new RTorrentTorrent
            {
                Hash = "HASH1",
                Name = "Test",
                Label = "movies",
                Trackers = new List<string> { "https://tracker.example.com/announce" }
            };
            var wrapper = new RTorrentItemWrapper(torrent);

            // Act
            var result = wrapper.IsIgnored(new List<string> { "example.com" });

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ReturnsFalse_WhenNoMatch()
        {
            // Arrange
            var torrent = new RTorrentTorrent
            {
                Hash = "HASH1",
                Name = "Test",
                Label = "movies",
                Trackers = new List<string> { "https://tracker.example.com/announce" }
            };
            var wrapper = new RTorrentItemWrapper(torrent);

            // Act
            var result = wrapper.IsIgnored(new List<string> { "other.com", "tv", "HASH2" });

            // Assert
            Assert.False(result);
        }
    }
}
