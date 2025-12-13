using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Shouldly;
using Xunit;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Tests.Models.Configuration.QueueCleaner;

public sealed class FailedImportConfigTests
{
    #region Validate - Valid Configurations

    [Fact]
    public void Validate_WithDisabledConfig_DoesNotThrow()
    {
        var config = new FailedImportConfig
        {
            MaxStrikes = 0
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WithValidMaxStrikesAndIncludePatterns_DoesNotThrow()
    {
        var config = new FailedImportConfig
        {
            MaxStrikes = 3,
            PatternMode = PatternMode.Include,
            Patterns = ["pattern1", "pattern2"]
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WithValidMaxStrikesAndExcludeMode_DoesNotThrow()
    {
        var config = new FailedImportConfig
        {
            MaxStrikes = 3,
            PatternMode = PatternMode.Exclude,
            Patterns = []
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WithHighMaxStrikes_DoesNotThrow()
    {
        var config = new FailedImportConfig
        {
            MaxStrikes = 100,
            PatternMode = PatternMode.Include,
            Patterns = ["pattern"]
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Validate - MaxStrikes Validation

    [Theory]
    [InlineData((ushort)1)]
    [InlineData((ushort)2)]
    public void Validate_WithMaxStrikesBetween1And2_ThrowsValidationException(ushort maxStrikes)
    {
        var config = new FailedImportConfig
        {
            MaxStrikes = maxStrikes,
            PatternMode = PatternMode.Include,
            Patterns = ["pattern"]
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("The minimum value for failed imports max strikes must be 3");
    }

    [Fact]
    public void Validate_WithMinimumValidMaxStrikes_DoesNotThrow()
    {
        var config = new FailedImportConfig
        {
            MaxStrikes = 3,
            PatternMode = PatternMode.Include,
            Patterns = ["pattern"]
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Validate - Pattern Mode Validation

    [Fact]
    public void Validate_WithIncludeModeAndNoPatterns_ThrowsValidationException()
    {
        var config = new FailedImportConfig
        {
            MaxStrikes = 3,
            PatternMode = PatternMode.Include,
            Patterns = []
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("At least one pattern must be specified when using the Include pattern mode");
    }

    [Fact]
    public void Validate_WithExcludeModeAndNoPatterns_DoesNotThrow()
    {
        var config = new FailedImportConfig
        {
            MaxStrikes = 3,
            PatternMode = PatternMode.Exclude,
            Patterns = []
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WithExcludeModeAndPatterns_DoesNotThrow()
    {
        var config = new FailedImportConfig
        {
            MaxStrikes = 3,
            PatternMode = PatternMode.Exclude,
            Patterns = ["excluded-pattern"]
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WithDisabledAndIncludeModeNoPatterns_DoesNotThrow()
    {
        // When MaxStrikes is 0 (disabled), patterns are not required
        var config = new FailedImportConfig
        {
            MaxStrikes = 0,
            PatternMode = PatternMode.Include,
            Patterns = []
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WithLowMaxStrikesAndIncludeModeNoPatterns_ThrowsMaxStrikesException()
    {
        // MaxStrikes validation happens before pattern validation
        var config = new FailedImportConfig
        {
            MaxStrikes = 2,
            PatternMode = PatternMode.Include,
            Patterns = []
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("The minimum value for failed imports max strikes must be 3");
    }

    #endregion
}
