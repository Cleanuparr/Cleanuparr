using Cleanuparr.Persistence.Models.Configuration.Seeker;
using Shouldly;
using Xunit;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

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

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldContain("at least");
    }

    [Fact]
    public void Validate_WithIntervalAboveMaximum_ThrowsValidationException()
    {
        var config = new SeekerConfig { SearchInterval = 31 };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
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

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldContain("Invalid search interval");
    }

    #endregion

    #region ToCronExpression

    [Theory]
    [InlineData((ushort)2, "0 */2 * * * ?")]
    [InlineData((ushort)5, "0 */5 * * * ?")]
    [InlineData((ushort)10, "0 */10 * * * ?")]
    [InlineData((ushort)30, "0 */30 * * * ?")]
    public void ToCronExpression_ReturnsCorrectCron(ushort interval, string expectedCron)
    {
        var config = new SeekerConfig { SearchInterval = interval };

        config.ToCronExpression().ShouldBe(expectedCron);
    }

    #endregion
}
