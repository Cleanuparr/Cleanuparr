using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Shouldly;
using Xunit;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Tests.Models.Configuration.DownloadCleaner;

public sealed class DownloadCleanerConfigTests
{
    #region Validate - Disabled Config

    [Fact]
    public void Validate_WhenDisabled_DoesNotThrow()
    {
        var config = new DownloadCleanerConfig
        {
            Enabled = false
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WhenDisabledWithNoFeatures_DoesNotThrow()
    {
        var config = new DownloadCleanerConfig
        {
            Enabled = false,
            Categories = [],
            UnlinkedEnabled = false
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Validate - No Features Configured

    [Fact]
    public void Validate_WhenEnabledWithNoFeatures_ThrowsValidationException()
    {
        var config = new DownloadCleanerConfig
        {
            Enabled = true,
            Categories = [],
            UnlinkedEnabled = false
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("No features are enabled");
    }

    [Fact]
    public void Validate_WhenEnabledWithUnlinkedEnabledButNoCategories_ThrowsValidationException()
    {
        var config = new DownloadCleanerConfig
        {
            Enabled = true,
            Categories = [],
            UnlinkedEnabled = true,
            UnlinkedCategories = [],
            UnlinkedTargetCategory = "target"
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("No features are enabled");
    }

    #endregion

    #region Validate - Categories Feature

    [Fact]
    public void Validate_WhenEnabledWithValidCategories_DoesNotThrow()
    {
        var config = new DownloadCleanerConfig
        {
            Enabled = true,
            Categories =
            [
                new SeedingRule { Name = "movies", MaxRatio = 2.0, MinSeedTime = 0, MaxSeedTime = -1, DeleteSourceFiles = true },
                new SeedingRule { Name = "tv", MaxRatio = 1.5, MinSeedTime = 24, MaxSeedTime = -1, DeleteSourceFiles = true }
            ],
            UnlinkedEnabled = false
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WhenEnabledWithDuplicateCategoryNames_ThrowsValidationException()
    {
        var config = new DownloadCleanerConfig
        {
            Enabled = true,
            Categories =
            [
                new SeedingRule { Name = "movies", MaxRatio = 2.0, MinSeedTime = 0, MaxSeedTime = -1, DeleteSourceFiles = true },
                new SeedingRule { Name = "movies", MaxRatio = 1.5, MinSeedTime = 24, MaxSeedTime = -1, DeleteSourceFiles = true }
            ],
            UnlinkedEnabled = false
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Duplicated clean category and privacy type combination found");
    }

    [Fact]
    public void Validate_WhenDuplicateCategoryNamesDifferentCase_ThrowsValidationException()
    {
        var config = new DownloadCleanerConfig
        {
            Enabled = true,
            Categories =
            [
                new SeedingRule { Name = "Movies", MaxRatio = 2.0, MinSeedTime = 0, MaxSeedTime = -1, DeleteSourceFiles = true },
                new SeedingRule { Name = "movies", MaxRatio = 1.5, MinSeedTime = 24, MaxSeedTime = -1, DeleteSourceFiles = true }
            ],
            UnlinkedEnabled = false
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Duplicated clean category and privacy type combination found");
    }

    [Fact]
    public void Validate_WhenSameCategoryWithBothAndPublic_ThrowsValidationException()
    {
        var config = new DownloadCleanerConfig
        {
            Enabled = true,
            Categories =
            [
                new SeedingRule { Name = "movies", PrivacyType = TorrentPrivacyType.Both, MaxRatio = 2.0, MinSeedTime = 0, MaxSeedTime = -1, DeleteSourceFiles = true },
                new SeedingRule { Name = "movies", PrivacyType = TorrentPrivacyType.Public, MaxRatio = 1.5, MinSeedTime = 24, MaxSeedTime = -1, DeleteSourceFiles = true }
            ],
            UnlinkedEnabled = false
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldContain("already covers all torrent types");
    }

    [Fact]
    public void Validate_WhenSameCategoryWithBothAndPrivate_ThrowsValidationException()
    {
        var config = new DownloadCleanerConfig
        {
            Enabled = true,
            Categories =
            [
                new SeedingRule { Name = "movies", PrivacyType = TorrentPrivacyType.Both, MaxRatio = 2.0, MinSeedTime = 0, MaxSeedTime = -1, DeleteSourceFiles = true },
                new SeedingRule { Name = "movies", PrivacyType = TorrentPrivacyType.Private, MaxRatio = 1.5, MinSeedTime = 24, MaxSeedTime = -1, DeleteSourceFiles = true }
            ],
            UnlinkedEnabled = false
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldContain("already covers all torrent types");
    }

    [Fact]
    public void Validate_WhenSameCategoryWithPublicAndPrivate_DoesNotThrow()
    {
        var config = new DownloadCleanerConfig
        {
            Enabled = true,
            Categories =
            [
                new SeedingRule { Name = "movies", PrivacyType = TorrentPrivacyType.Public, MaxRatio = 2.0, MinSeedTime = 0, MaxSeedTime = -1, DeleteSourceFiles = true },
                new SeedingRule { Name = "movies", PrivacyType = TorrentPrivacyType.Private, MaxRatio = 1.5, MinSeedTime = 24, MaxSeedTime = -1, DeleteSourceFiles = true }
            ],
            UnlinkedEnabled = false
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WhenEnabledWithInvalidCategory_ThrowsValidationException()
    {
        var config = new DownloadCleanerConfig
        {
            Enabled = true,
            Categories =
            [
                new SeedingRule { Name = "", MaxRatio = 2.0, MinSeedTime = 0, MaxSeedTime = -1, DeleteSourceFiles = true }
            ],
            UnlinkedEnabled = false
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Category name can not be empty");
    }

    #endregion

    #region Validate - Unlinked Feature

    [Fact]
    public void Validate_WhenEnabledWithValidUnlinkedConfig_DoesNotThrow()
    {
        var config = new DownloadCleanerConfig
        {
            Enabled = true,
            Categories = [],
            UnlinkedEnabled = true,
            UnlinkedTargetCategory = "cleanuparr-unlinked",
            UnlinkedCategories = ["movies", "tv"]
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WhenUnlinkedEnabledWithEmptyTargetCategory_ThrowsValidationException()
    {
        // Need valid categories to pass the "no features enabled" check first
        var config = new DownloadCleanerConfig
        {
            Enabled = true,
            Categories =
            [
                new SeedingRule { Name = "movies", MaxRatio = 2.0, MinSeedTime = 0, MaxSeedTime = -1, DeleteSourceFiles = true }
            ],
            UnlinkedEnabled = true,
            UnlinkedTargetCategory = "",
            UnlinkedCategories = ["tv"]
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("unlinked target category is required");
    }

    [Fact]
    public void Validate_WhenUnlinkedEnabledWithNoUnlinkedCategories_ThrowsValidationException()
    {
        // Need valid categories to pass the "no features enabled" check first
        var config = new DownloadCleanerConfig
        {
            Enabled = true,
            Categories =
            [
                new SeedingRule { Name = "movies", MaxRatio = 2.0, MinSeedTime = 0, MaxSeedTime = -1, DeleteSourceFiles = true }
            ],
            UnlinkedEnabled = true,
            UnlinkedTargetCategory = "cleanuparr-unlinked",
            UnlinkedCategories = []
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("No unlinked categories configured");
    }

    [Fact]
    public void Validate_WhenUnlinkedTargetCategoryInUnlinkedCategories_ThrowsValidationException()
    {
        var config = new DownloadCleanerConfig
        {
            Enabled = true,
            Categories = [],
            UnlinkedEnabled = true,
            UnlinkedTargetCategory = "cleanuparr-unlinked",
            UnlinkedCategories = ["movies", "cleanuparr-unlinked"]
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("The unlinked target category should not be present in unlinked categories");
    }

    [Fact]
    public void Validate_WhenUnlinkedCategoriesContainsEmpty_ThrowsValidationException()
    {
        var config = new DownloadCleanerConfig
        {
            Enabled = true,
            Categories = [],
            UnlinkedEnabled = true,
            UnlinkedTargetCategory = "cleanuparr-unlinked",
            UnlinkedCategories = ["movies", ""]
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Empty unlinked category filter found");
    }

    [Fact]
    public void Validate_WhenUnlinkedIgnoredRootDirDoesNotExist_ThrowsValidationException()
    {
        var config = new DownloadCleanerConfig
        {
            Enabled = true,
            Categories = [],
            UnlinkedEnabled = true,
            UnlinkedTargetCategory = "cleanuparr-unlinked",
            UnlinkedCategories = ["movies"],
            UnlinkedIgnoredRootDirs = ["/non/existent/directory"]
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldContain("root directory does not exist");
    }

    [Fact]
    public void Validate_WhenUnlinkedIgnoredRootDirIsEmpty_DoesNotThrow()
    {
        var config = new DownloadCleanerConfig
        {
            Enabled = true,
            Categories = [],
            UnlinkedEnabled = true,
            UnlinkedTargetCategory = "cleanuparr-unlinked",
            UnlinkedCategories = ["movies"],
            UnlinkedIgnoredRootDirs = []
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Validate - Combined Features

    [Fact]
    public void Validate_WhenBothFeaturesEnabled_DoesNotThrow()
    {
        var config = new DownloadCleanerConfig
        {
            Enabled = true,
            Categories =
            [
                new SeedingRule { Name = "movies", MaxRatio = 2.0, MinSeedTime = 0, MaxSeedTime = -1, DeleteSourceFiles = true }
            ],
            UnlinkedEnabled = true,
            UnlinkedTargetCategory = "cleanuparr-unlinked",
            UnlinkedCategories = ["tv"]
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Default Values

    [Fact]
    public void CronExpression_HasDefaultValue()
    {
        var config = new DownloadCleanerConfig();

        config.CronExpression.ShouldBe("0 0 * * * ?");
    }

    [Fact]
    public void UnlinkedTargetCategory_HasDefaultValue()
    {
        var config = new DownloadCleanerConfig();

        config.UnlinkedTargetCategory.ShouldBe("cleanuparr-unlinked");
    }

    #endregion
}
