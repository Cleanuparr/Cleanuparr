using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Shouldly;
using Xunit;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Tests.Models.Configuration.QueueCleaner;

public sealed class QueueCleanerConfigTests
{
    #region Validate - Valid Configurations

    [Fact]
    public void Validate_WithDefaultConfig_DoesNotThrow()
    {
        var config = new QueueCleanerConfig();

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WithValidStallRules_DoesNotThrow()
    {
        var config = new QueueCleanerConfig
        {
            StallRules =
            [
                new StallRule { Name = "rule1", MaxStrikes = 3, Enabled = true },
                new StallRule { Name = "rule2", MaxStrikes = 5, Enabled = true }
            ]
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WithValidSlowRules_DoesNotThrow()
    {
        var config = new QueueCleanerConfig
        {
            SlowRules =
            [
                new SlowRule { Name = "slow1", MaxStrikes = 3, MinSpeed = "100KB", Enabled = true },
                new SlowRule { Name = "slow2", MaxStrikes = 5, MaxTimeHours = 24, Enabled = true }
            ]
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Validate - DownloadingMetadataMaxStrikes Validation

    [Fact]
    public void Validate_WithZeroDownloadingMetadataMaxStrikes_DoesNotThrow()
    {
        var config = new QueueCleanerConfig
        {
            DownloadingMetadataMaxStrikes = 0
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WithMinimumValidDownloadingMetadataMaxStrikes_DoesNotThrow()
    {
        var config = new QueueCleanerConfig
        {
            DownloadingMetadataMaxStrikes = 3
        };

        Should.NotThrow(() => config.Validate());
    }

    [Theory]
    [InlineData((ushort)1)]
    [InlineData((ushort)2)]
    public void Validate_WithDownloadingMetadataMaxStrikesBetween1And2_ThrowsValidationException(ushort maxStrikes)
    {
        var config = new QueueCleanerConfig
        {
            DownloadingMetadataMaxStrikes = maxStrikes
        };

        var exception = Should.Throw<System.ComponentModel.DataAnnotations.ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("the minimum value for downloading metadata max strikes must be 3");
    }

    [Theory]
    [InlineData((ushort)3)]
    [InlineData((ushort)5)]
    [InlineData((ushort)100)]
    public void Validate_WithValidDownloadingMetadataMaxStrikes_DoesNotThrow(ushort maxStrikes)
    {
        var config = new QueueCleanerConfig
        {
            DownloadingMetadataMaxStrikes = maxStrikes
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Validate - FailedImport Validation

    [Fact]
    public void Validate_WithInvalidFailedImportConfig_ThrowsValidationException()
    {
        var config = new QueueCleanerConfig
        {
            FailedImport = new FailedImportConfig
            {
                MaxStrikes = 1 // Invalid - must be 0 or >= 3
            }
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("The minimum value for failed imports max strikes must be 3");
    }

    #endregion

    #region Validate - StallRule Validation

    [Fact]
    public void Validate_WithInvalidStallRule_ThrowsValidationException()
    {
        var config = new QueueCleanerConfig
        {
            StallRules =
            [
                new StallRule { Name = "", MaxStrikes = 3 } // Invalid name
            ]
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Rule name cannot be empty");
    }

    [Fact]
    public void Validate_WithDuplicateEnabledStallRuleNames_ThrowsValidationException()
    {
        var config = new QueueCleanerConfig
        {
            StallRules =
            [
                new StallRule { Name = "duplicate", MaxStrikes = 3, Enabled = true },
                new StallRule { Name = "duplicate", MaxStrikes = 5, Enabled = true }
            ]
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Duplicate stall rule names found");
    }

    [Fact]
    public void Validate_WithDuplicateDisabledStallRuleNames_DoesNotThrow()
    {
        var config = new QueueCleanerConfig
        {
            StallRules =
            [
                new StallRule { Name = "duplicate", MaxStrikes = 3, Enabled = false },
                new StallRule { Name = "duplicate", MaxStrikes = 5, Enabled = false }
            ]
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WithDuplicateButOneDisabledStallRule_DoesNotThrow()
    {
        var config = new QueueCleanerConfig
        {
            StallRules =
            [
                new StallRule { Name = "duplicate", MaxStrikes = 3, Enabled = true },
                new StallRule { Name = "duplicate", MaxStrikes = 5, Enabled = false }
            ]
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Validate - SlowRule Validation

    [Fact]
    public void Validate_WithInvalidSlowRule_ThrowsValidationException()
    {
        var config = new QueueCleanerConfig
        {
            SlowRules =
            [
                new SlowRule { Name = "", MaxStrikes = 3, MinSpeed = "100KB" } // Invalid name
            ]
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Rule name cannot be empty");
    }

    [Fact]
    public void Validate_WithDuplicateEnabledSlowRuleNames_ThrowsValidationException()
    {
        var config = new QueueCleanerConfig
        {
            SlowRules =
            [
                new SlowRule { Name = "duplicate", MaxStrikes = 3, MinSpeed = "100KB", Enabled = true },
                new SlowRule { Name = "duplicate", MaxStrikes = 5, MaxTimeHours = 24, Enabled = true }
            ]
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Duplicate slow rule names found");
    }

    [Fact]
    public void Validate_WithDuplicateDisabledSlowRuleNames_DoesNotThrow()
    {
        var config = new QueueCleanerConfig
        {
            SlowRules =
            [
                new SlowRule { Name = "duplicate", MaxStrikes = 3, MinSpeed = "100KB", Enabled = false },
                new SlowRule { Name = "duplicate", MaxStrikes = 5, MaxTimeHours = 24, Enabled = false }
            ]
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Validate - Mixed Rules

    [Fact]
    public void Validate_WithSameNameAcrossStallAndSlowRules_DoesNotThrow()
    {
        // Same name is allowed between stall and slow rules
        var config = new QueueCleanerConfig
        {
            StallRules =
            [
                new StallRule { Name = "samename", MaxStrikes = 3, Enabled = true }
            ],
            SlowRules =
            [
                new SlowRule { Name = "samename", MaxStrikes = 3, MinSpeed = "100KB", Enabled = true }
            ]
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Default Values

    [Fact]
    public void CronExpression_HasDefaultValue()
    {
        var config = new QueueCleanerConfig();

        config.CronExpression.ShouldBe("0 0/5 * * * ?");
    }

    [Fact]
    public void UseAdvancedScheduling_DefaultsToFalse()
    {
        var config = new QueueCleanerConfig();

        config.UseAdvancedScheduling.ShouldBeFalse();
    }

    #endregion
}
