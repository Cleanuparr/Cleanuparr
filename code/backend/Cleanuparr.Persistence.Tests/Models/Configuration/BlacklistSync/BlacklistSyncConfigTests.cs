using Cleanuparr.Persistence.Models.Configuration.BlacklistSync;
using Shouldly;
using Xunit;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Tests.Models.Configuration.BlacklistSync;

public sealed class BlacklistSyncConfigTests
{
    #region Validate - Disabled Config

    [Fact]
    public void Validate_WhenDisabled_DoesNotThrow()
    {
        var config = new BlacklistSyncConfig
        {
            Enabled = false,
            BlacklistPath = null
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WhenDisabledWithEmptyPath_DoesNotThrow()
    {
        var config = new BlacklistSyncConfig
        {
            Enabled = false,
            BlacklistPath = ""
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Validate - Enabled Config Path Validation

    [Fact]
    public void Validate_WhenEnabledWithNullPath_ThrowsValidationException()
    {
        var config = new BlacklistSyncConfig
        {
            Enabled = true,
            BlacklistPath = null
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Blacklist sync is enabled but the path is not configured");
    }

    [Fact]
    public void Validate_WhenEnabledWithEmptyPath_ThrowsValidationException()
    {
        var config = new BlacklistSyncConfig
        {
            Enabled = true,
            BlacklistPath = ""
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Blacklist sync is enabled but the path is not configured");
    }

    [Fact]
    public void Validate_WhenEnabledWithWhitespacePath_ThrowsValidationException()
    {
        var config = new BlacklistSyncConfig
        {
            Enabled = true,
            BlacklistPath = "   "
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Blacklist sync is enabled but the path is not configured");
    }

    #endregion

    #region Validate - URL Paths

    [Theory]
    [InlineData("http://example.com/blacklist.txt")]
    [InlineData("https://example.com/blacklist.txt")]
    [InlineData("http://localhost:8080/api/blacklist")]
    [InlineData("https://raw.githubusercontent.com/user/repo/main/blacklist.txt")]
    public void Validate_WhenEnabledWithValidHttpUrl_DoesNotThrow(string url)
    {
        var config = new BlacklistSyncConfig
        {
            Enabled = true,
            BlacklistPath = url
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Validate - Invalid Paths

    [Fact]
    public void Validate_WhenEnabledWithNonExistentFilePath_ThrowsValidationException()
    {
        var config = new BlacklistSyncConfig
        {
            Enabled = true,
            BlacklistPath = "/non/existent/path/blacklist.txt"
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Blacklist path must be a valid URL or an existing local file path");
    }

    [Fact]
    public void Validate_WhenEnabledWithInvalidPath_ThrowsValidationException()
    {
        var config = new BlacklistSyncConfig
        {
            Enabled = true,
            BlacklistPath = "not-a-valid-url-or-path"
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Blacklist path must be a valid URL or an existing local file path");
    }

    [Theory]
    [InlineData("ftp://example.com/blacklist.txt")]
    [InlineData("file:///path/to/file")]
    public void Validate_WhenEnabledWithNonHttpUrl_ThrowsValidationException(string url)
    {
        var config = new BlacklistSyncConfig
        {
            Enabled = true,
            BlacklistPath = url
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Blacklist path must be a valid URL or an existing local file path");
    }

    #endregion

    #region CronExpression Default

    [Fact]
    public void CronExpression_HasDefaultValue()
    {
        var config = new BlacklistSyncConfig();

        config.CronExpression.ShouldBe("0 0 * * * ?");
    }

    #endregion
}
