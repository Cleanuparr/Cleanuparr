using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Persistence.Models.Configuration.Seeker;
using Shouldly;
using Xunit;

namespace Cleanuparr.Persistence.Tests.Models.Configuration.Seeker;

public sealed class SeekerConfigTests
{
    #region Validate - Valid Configurations

    [Fact]
    public void Validate_WithDefaultConfig_DoesNotThrow()
    {
        var config = new SeekerConfig();

        Should.NotThrow(() => config.Validate());
    }

    [Theory]
    [InlineData((ushort)2)]
    [InlineData((ushort)3)]
    [InlineData((ushort)4)]
    [InlineData((ushort)5)]
    [InlineData((ushort)6)]
    [InlineData((ushort)10)]
    [InlineData((ushort)12)]
    [InlineData((ushort)15)]
    [InlineData((ushort)20)]
    [InlineData((ushort)30)]
    [InlineData((ushort)60)]
    [InlineData((ushort)120)]
    [InlineData((ushort)180)]
    [InlineData((ushort)240)]
    [InlineData((ushort)360)]
    public void Validate_WithValidIntervals_DoesNotThrow(ushort interval)
    {
        var config = new SeekerConfig { SearchInterval = interval };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Validate - Invalid Configurations

    [Fact]
    public void Validate_WithIntervalBelowMinimum_ThrowsValidationException()
    {
        var config = new SeekerConfig { SearchInterval = 1 };

        var exception = Should.Throw<CustomValidationException>(() => config.Validate());
        exception.Message.ShouldContain("at least");
    }

    [Fact]
    public void Validate_WithIntervalAboveMaximum_ThrowsValidationException()
    {
        var config = new SeekerConfig { SearchInterval = 361 };

        var exception = Should.Throw<CustomValidationException>(() => config.Validate());
        exception.Message.ShouldContain("at most");
    }

    [Theory]
    [InlineData((ushort)7)]
    [InlineData((ushort)8)]
    [InlineData((ushort)9)]
    [InlineData((ushort)11)]
    [InlineData((ushort)13)]
    [InlineData((ushort)14)]
    public void Validate_WithNonDivisorInterval_ThrowsValidationException(ushort interval)
    {
        var config = new SeekerConfig { SearchInterval = interval };

        var exception = Should.Throw<CustomValidationException>(() => config.Validate());
        exception.Message.ShouldContain("Invalid search interval");
    }

    #endregion

    #region ToCronExpression

    [Theory]
    [InlineData((ushort)2, "0 */2 * * * ?")]
    [InlineData((ushort)5, "0 */5 * * * ?")]
    [InlineData((ushort)10, "0 */10 * * * ?")]
    [InlineData((ushort)30, "0 */30 * * * ?")]
    [InlineData((ushort)60, "0 0 * * * ?")]
    [InlineData((ushort)120, "0 0 */2 * * ?")]
    [InlineData((ushort)180, "0 0 */3 * * ?")]
    [InlineData((ushort)240, "0 0 */4 * * ?")]
    [InlineData((ushort)360, "0 0 */6 * * ?")]
    public void ToCronExpression_ReturnsCorrectCron(ushort interval, string expectedCron)
    {
        var config = new SeekerConfig { SearchInterval = interval };

        config.ToCronExpression().ShouldBe(expectedCron);
    }

    #endregion

    #region Quiet Hours

    [Fact]
    public void Validate_WithEqualQuietHours_ThrowsValidationException()
    {
        SeekerConfig config = new()
        {
            ProactiveSearchEnabled = true,
            QuietHoursEnabled = true,
            QuietHoursStart = new TimeOnly(5, 0),
            QuietHoursEnd = new TimeOnly(5, 0)
        };

        Should.Throw<CustomValidationException>(() => config.Validate());
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void Validate_WithEqualQuietHoursThatNeverApply_DoesNotThrow(bool searchEnabled, bool proactiveSearchEnabled, bool quietHoursEnabled)
    {
        SeekerConfig config = new()
        {
            SearchEnabled = searchEnabled,
            ProactiveSearchEnabled = proactiveSearchEnabled,
            QuietHoursEnabled = quietHoursEnabled,
            QuietHoursStart = new TimeOnly(5, 0),
            QuietHoursEnd = new TimeOnly(5, 0)
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void IsQuietTime_WhenDisabled_ReturnsFalse()
    {
        SeekerConfig config = new() { QuietHoursEnabled = false };

        config.IsQuietTime(new TimeOnly(23, 30)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(0, 59, false)]
    [InlineData(1, 0, true)]
    [InlineData(4, 59, true)]
    [InlineData(5, 0, false)]
    public void IsQuietTime_WithSameDayWindow_RespectsBounds(int hour, int minute, bool expected)
    {
        SeekerConfig config = new()
        {
            QuietHoursEnabled = true,
            QuietHoursStart = new TimeOnly(1, 0),
            QuietHoursEnd = new TimeOnly(5, 0)
        };

        config.IsQuietTime(new TimeOnly(hour, minute)).ShouldBe(expected);
    }

    [Theory]
    [InlineData(22, 59, false)]
    [InlineData(23, 0, true)]
    [InlineData(0, 0, true)]
    [InlineData(6, 59, true)]
    [InlineData(7, 0, false)]
    public void IsQuietTime_WithOvernightWindow_RespectsBounds(int hour, int minute, bool expected)
    {
        SeekerConfig config = new()
        {
            QuietHoursEnabled = true,
            QuietHoursStart = new TimeOnly(23, 0),
            QuietHoursEnd = new TimeOnly(7, 0)
        };

        config.IsQuietTime(new TimeOnly(hour, minute)).ShouldBe(expected);
    }

    #endregion
}
