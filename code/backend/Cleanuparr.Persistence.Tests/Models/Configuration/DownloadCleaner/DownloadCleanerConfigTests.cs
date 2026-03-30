using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Shouldly;
using Xunit;

namespace Cleanuparr.Persistence.Tests.Models.Configuration.DownloadCleaner;

public sealed class DownloadCleanerConfigTests
{
    #region Validate

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
    public void Validate_WhenEnabled_DoesNotThrow()
    {
        var config = new DownloadCleanerConfig
        {
            Enabled = true
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
    public void IgnoredDownloads_HasDefaultEmptyList()
    {
        var config = new DownloadCleanerConfig();

        config.IgnoredDownloads.ShouldBeEmpty();
    }

    #endregion
}
