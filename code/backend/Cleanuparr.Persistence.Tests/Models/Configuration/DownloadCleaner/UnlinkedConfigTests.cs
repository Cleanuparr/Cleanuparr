using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Shouldly;
using Xunit;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Tests.Models.Configuration.DownloadCleaner;

public sealed class UnlinkedConfigTests
{
    #region Default Values

    [Fact]
    public void Defaults_EnabledIsFalse()
    {
        var config = new UnlinkedConfig();
        config.Enabled.ShouldBeFalse();
    }

    [Fact]
    public void Defaults_TargetCategoryIsSet()
    {
        var config = new UnlinkedConfig();
        config.TargetCategory.ShouldBe("cleanuparr-unlinked");
    }

    [Fact]
    public void Defaults_CategoriesIsEmpty()
    {
        var config = new UnlinkedConfig();
        config.Categories.ShouldBeEmpty();
    }

    [Fact]
    public void Defaults_DownloadDirectorySourceIsNull()
    {
        var config = new UnlinkedConfig();
        config.DownloadDirectorySource.ShouldBeNull();
    }

    [Fact]
    public void Defaults_DownloadDirectoryTargetIsNull()
    {
        var config = new UnlinkedConfig();
        config.DownloadDirectoryTarget.ShouldBeNull();
    }

    #endregion

    #region Validate - Disabled

    [Fact]
    public void Validate_WhenDisabled_DoesNotThrow()
    {
        var config = new UnlinkedConfig
        {
            Enabled = false,
            TargetCategory = "",
            Categories = []
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Validate - Enabled

    [Fact]
    public void Validate_WhenEnabled_WithValidConfig_DoesNotThrow()
    {
        var config = new UnlinkedConfig
        {
            Enabled = true,
            TargetCategory = "cleanuparr-unlinked",
            Categories = ["movies", "tv"]
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WhenEnabled_WithEmptyTargetCategory_ThrowsValidationException()
    {
        var config = new UnlinkedConfig
        {
            Enabled = true,
            TargetCategory = "",
            Categories = ["movies"]
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Unlinked target category is required");
    }

    [Fact]
    public void Validate_WhenEnabled_WithEmptyCategories_ThrowsValidationException()
    {
        var config = new UnlinkedConfig
        {
            Enabled = true,
            TargetCategory = "cleanuparr-unlinked",
            Categories = []
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("No unlinked categories configured");
    }

    [Fact]
    public void Validate_WhenEnabled_WithTargetCategoryInCategories_ThrowsValidationException()
    {
        var config = new UnlinkedConfig
        {
            Enabled = true,
            TargetCategory = "cleanuparr-unlinked",
            Categories = ["movies", "cleanuparr-unlinked"]
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("The unlinked target category should not be present in unlinked categories");
    }

    [Fact]
    public void Validate_WhenEnabled_WithEmptyCategoryEntry_ThrowsValidationException()
    {
        var config = new UnlinkedConfig
        {
            Enabled = true,
            TargetCategory = "cleanuparr-unlinked",
            Categories = ["movies", ""]
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Empty unlinked category filter found");
    }

    #endregion

    #region Validate - Directory Mapping

    [Fact]
    public void Validate_WhenEnabled_WithOnlySourceSet_ThrowsValidationException()
    {
        var config = new UnlinkedConfig
        {
            Enabled = true,
            TargetCategory = "cleanuparr-unlinked",
            Categories = ["movies"],
            DownloadDirectorySource = "/downloads",
            DownloadDirectoryTarget = null
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Both download directory source and target must be set, or both must be empty");
    }

    [Fact]
    public void Validate_WhenEnabled_WithOnlyTargetSet_ThrowsValidationException()
    {
        var config = new UnlinkedConfig
        {
            Enabled = true,
            TargetCategory = "cleanuparr-unlinked",
            Categories = ["movies"],
            DownloadDirectorySource = null,
            DownloadDirectoryTarget = "/data/downloads"
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Both download directory source and target must be set, or both must be empty");
    }

    [Fact]
    public void Validate_WhenEnabled_WithBothDirsSet_DoesNotThrow()
    {
        var config = new UnlinkedConfig
        {
            Enabled = true,
            TargetCategory = "cleanuparr-unlinked",
            Categories = ["movies"],
            DownloadDirectorySource = "/downloads",
            DownloadDirectoryTarget = "/data/downloads"
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WhenEnabled_WithBothDirsEmpty_DoesNotThrow()
    {
        var config = new UnlinkedConfig
        {
            Enabled = true,
            TargetCategory = "cleanuparr-unlinked",
            Categories = ["movies"],
            DownloadDirectorySource = null,
            DownloadDirectoryTarget = null
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Validate - Ignored Root Dirs

    [Fact]
    public void Validate_WhenEnabled_WithNonExistentIgnoredRootDir_ThrowsValidationException()
    {
        var config = new UnlinkedConfig
        {
            Enabled = true,
            TargetCategory = "cleanuparr-unlinked",
            Categories = ["movies"],
            IgnoredRootDirs = ["/non/existent/path/that/should/not/exist"]
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldContain("root directory does not exist");
    }

    [Fact]
    public void Validate_WhenEnabled_WithEmptyIgnoredRootDirs_DoesNotThrow()
    {
        var config = new UnlinkedConfig
        {
            Enabled = true,
            TargetCategory = "cleanuparr-unlinked",
            Categories = ["movies"],
            IgnoredRootDirs = []
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WhenEnabled_SkipsEmptyStringInIgnoredRootDirs()
    {
        var config = new UnlinkedConfig
        {
            Enabled = true,
            TargetCategory = "cleanuparr-unlinked",
            Categories = ["movies"],
            IgnoredRootDirs = [""]
        };

        // Empty strings are filtered out, so this should not throw
        Should.NotThrow(() => config.Validate());
    }

    #endregion
}
