using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Shouldly;
using Xunit;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Tests.Models.Configuration.DownloadCleaner;

public sealed class DeadTorrentConfigTests
{
    [Fact]
    public void Defaults_EnabledIsFalse()
    {
        var config = new DeadTorrentConfig();
        config.Enabled.ShouldBeFalse();
    }

    [Fact]
    public void Defaults_TargetCategoryIsSet()
    {
        var config = new DeadTorrentConfig();
        config.TargetCategory.ShouldBe("cleanuparr-dead");
    }

    [Fact]
    public void Validate_WhenDisabled_DoesNotThrow()
    {
        var config = new DeadTorrentConfig
        {
            Enabled = false,
            TargetCategory = "",
            Categories = [],
            MaxStrikes = 0
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WhenEnabled_WithValidConfig_DoesNotThrow()
    {
        var config = new DeadTorrentConfig
        {
            Enabled = true,
            TargetCategory = "cleanuparr-dead",
            Categories = ["movies", "tv"],
            MaxStrikes = 3
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WhenEnabled_WithEmptyTargetCategory_ThrowsValidationException()
    {
        var config = new DeadTorrentConfig
        {
            Enabled = true,
            TargetCategory = "",
            Categories = ["movies"],
            MaxStrikes = 3
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Dead torrent target category is required");
    }

    [Fact]
    public void Validate_WhenEnabled_WithEmptyCategories_ThrowsValidationException()
    {
        var config = new DeadTorrentConfig
        {
            Enabled = true,
            TargetCategory = "cleanuparr-dead",
            Categories = [],
            MaxStrikes = 3
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("No dead torrent categories configured");
    }

    [Fact]
    public void Validate_WhenEnabled_WithTargetCategoryInCategories_ThrowsValidationException()
    {
        var config = new DeadTorrentConfig
        {
            Enabled = true,
            TargetCategory = "cleanuparr-dead",
            Categories = ["movies", "cleanuparr-dead"],
            MaxStrikes = 3
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("The dead torrent target category should not be present in dead torrent categories");
    }

    [Fact]
    public void Validate_WhenEnabled_WithEmptyCategoryEntry_ThrowsValidationException()
    {
        var config = new DeadTorrentConfig
        {
            Enabled = true,
            TargetCategory = "cleanuparr-dead",
            Categories = ["movies", ""],
            MaxStrikes = 3
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Empty dead torrent category filter found");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Validate_WhenEnabled_WithStrikesBelowMinimum_ThrowsValidationException(ushort strikes)
    {
        var config = new DeadTorrentConfig
        {
            Enabled = true,
            TargetCategory = "cleanuparr-dead",
            Categories = ["movies"],
            MaxStrikes = strikes
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Dead torrent max strikes must be at least 3");
    }
}
