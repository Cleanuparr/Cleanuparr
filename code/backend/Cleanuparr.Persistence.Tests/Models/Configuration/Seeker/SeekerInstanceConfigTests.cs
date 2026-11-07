using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Persistence.Models.Configuration.Seeker;
using Shouldly;
using Xunit;

namespace Cleanuparr.Persistence.Tests.Models.Configuration.Seeker;

public sealed class SeekerInstanceConfigTests
{
    #region Validate - Valid Configurations

    [Fact]
    public void Validate_WithDefaultConfig_DoesNotThrow()
    {
        var config = new SeekerInstanceConfig();

        Should.NotThrow(() => config.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(168)]
    public void Validate_WithBoundaryRecentGrabWindowHours_DoesNotThrow(int hours)
    {
        var config = new SeekerInstanceConfig { RecentGrabWindowHours = hours };

        Should.NotThrow(() => config.Validate());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    [InlineData(365)]
    public void Validate_WithBoundaryRestMaxDays_DoesNotThrow(int days)
    {
        var config = new SeekerInstanceConfig { RestMaxDays = days };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Validate - Invalid Configurations

    [Theory]
    [InlineData(-1)]
    [InlineData(169)]
    public void Validate_WithOutOfRangeRecentGrabWindowHours_ThrowsValidationException(int hours)
    {
        var config = new SeekerInstanceConfig { RecentGrabWindowHours = hours };

        var exception = Should.Throw<CustomValidationException>(() => config.Validate());
        exception.Message.ShouldContain("between 0 and 168");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(366)]
    public void Validate_WithOutOfRangeRestMaxDays_ThrowsValidationException(int days)
    {
        var config = new SeekerInstanceConfig { RestMaxDays = days };

        var exception = Should.Throw<CustomValidationException>(() => config.Validate());
        exception.Message.ShouldContain("between 1 and 365");
    }

    #endregion
}
