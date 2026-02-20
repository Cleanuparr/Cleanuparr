using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Shouldly;
using Xunit;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Tests.Models.Configuration.DownloadCleaner;

public sealed class SeedingRuleTests
{
    #region Default Values

    [Fact]
    public void PrivacyType_DefaultsToPublic()
    {
        var rule = new SeedingRule
        {
            Name = "test",
            MaxRatio = -1,
            MinSeedTime = 0,
            MaxSeedTime = 24,
            DeleteSourceFiles = false
        };

        rule.PrivacyType.ShouldBe(TorrentPrivacyType.Public);
    }

    #endregion

    #region Validate - Valid Configurations

    [Fact]
    public void Validate_WithValidMaxRatio_DoesNotThrow()
    {
        var config = new SeedingRule
        {
            Name = "test-category",
            MaxRatio = 2.0,
            MinSeedTime = 0,
            MaxSeedTime = -1,
            DeleteSourceFiles = true
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WithValidMaxSeedTime_DoesNotThrow()
    {
        var config = new SeedingRule
        {
            Name = "test-category",
            MaxRatio = -1,
            MinSeedTime = 0,
            MaxSeedTime = 24,
            DeleteSourceFiles = true
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WithBothMaxRatioAndMaxSeedTime_DoesNotThrow()
    {
        var config = new SeedingRule
        {
            Name = "test-category",
            MaxRatio = 2.0,
            MinSeedTime = 1,
            MaxSeedTime = 48,
            DeleteSourceFiles = true
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WithZeroMaxRatio_DoesNotThrow()
    {
        var config = new SeedingRule
        {
            Name = "test-category",
            MaxRatio = 0,
            MinSeedTime = 0,
            MaxSeedTime = -1,
            DeleteSourceFiles = true
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WithZeroMaxSeedTime_DoesNotThrow()
    {
        var config = new SeedingRule
        {
            Name = "test-category",
            MaxRatio = -1,
            MinSeedTime = 0,
            MaxSeedTime = 0,
            DeleteSourceFiles = true
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Validate - Name Validation

    [Fact]
    public void Validate_WithEmptyName_ThrowsValidationException()
    {
        var config = new SeedingRule
        {
            Name = "",
            MaxRatio = 2.0,
            MinSeedTime = 0,
            MaxSeedTime = -1,
            DeleteSourceFiles = true
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Category name can not be empty");
    }

    [Fact]
    public void Validate_WithWhitespaceName_ThrowsValidationException()
    {
        var config = new SeedingRule
        {
            Name = "   ",
            MaxRatio = 2.0,
            MinSeedTime = 0,
            MaxSeedTime = -1,
            DeleteSourceFiles = true
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Category name can not be empty");
    }

    [Fact]
    public void Validate_WithTabOnlyName_ThrowsValidationException()
    {
        var config = new SeedingRule
        {
            Name = "\t",
            MaxRatio = 2.0,
            MinSeedTime = 0,
            MaxSeedTime = -1,
            DeleteSourceFiles = true
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Category name can not be empty");
    }

    #endregion

    #region Validate - MaxRatio and MaxSeedTime Validation

    [Fact]
    public void Validate_WithBothNegative_ThrowsValidationException()
    {
        var config = new SeedingRule
        {
            Name = "test-category",
            MaxRatio = -1,
            MinSeedTime = 0,
            MaxSeedTime = -1,
            DeleteSourceFiles = true
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Either max ratio or max seed time must be set to a non-negative value");
    }

    [Theory]
    [InlineData(-1, -0.1)]
    [InlineData(-0.5, -1)]
    [InlineData(-100, -100)]
    public void Validate_WithVariousNegativeValues_ThrowsValidationException(double maxRatio, double maxSeedTime)
    {
        var config = new SeedingRule
        {
            Name = "test-category",
            MaxRatio = maxRatio,
            MinSeedTime = 0,
            MaxSeedTime = maxSeedTime,
            DeleteSourceFiles = true
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Either max ratio or max seed time must be set to a non-negative value");
    }

    #endregion

    #region Validate - MinSeedTime Validation

    [Fact]
    public void Validate_WithNegativeMinSeedTime_ThrowsValidationException()
    {
        var config = new SeedingRule
        {
            Name = "test-category",
            MaxRatio = 2.0,
            MinSeedTime = -1,
            MaxSeedTime = -1,
            DeleteSourceFiles = true
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Min seed time can not be negative");
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_WithVariousNegativeMinSeedTime_ThrowsValidationException(double minSeedTime)
    {
        var config = new SeedingRule
        {
            Name = "test-category",
            MaxRatio = 2.0,
            MinSeedTime = minSeedTime,
            MaxSeedTime = -1,
            DeleteSourceFiles = true
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Min seed time can not be negative");
    }

    [Fact]
    public void Validate_WithZeroMinSeedTime_DoesNotThrow()
    {
        var config = new SeedingRule
        {
            Name = "test-category",
            MaxRatio = 2.0,
            MinSeedTime = 0,
            MaxSeedTime = -1,
            DeleteSourceFiles = true
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WithPositiveMinSeedTime_DoesNotThrow()
    {
        var config = new SeedingRule
        {
            Name = "test-category",
            MaxRatio = 2.0,
            MinSeedTime = 24,
            MaxSeedTime = -1,
            DeleteSourceFiles = true
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    [Fact]
    public void DeleteSourceFiles_CanBeSetToFalse()
    {
        var config = new SeedingRule
        {
            Name = "test-category",
            MaxRatio = 2.0,
            MinSeedTime = 0,
            MaxSeedTime = -1,
            DeleteSourceFiles = false
        };

        config.DeleteSourceFiles.ShouldBeFalse();
    }
}
