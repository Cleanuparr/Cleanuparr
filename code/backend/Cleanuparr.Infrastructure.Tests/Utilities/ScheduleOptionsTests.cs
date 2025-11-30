using Cleanuparr.Infrastructure.Models;
using Cleanuparr.Infrastructure.Utilities;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Utilities;

public class ScheduleOptionsTests
{
    [Fact]
    public void GetValidValues_Seconds_Returns30()
    {
        // Act
        var result = ScheduleOptions.GetValidValues(ScheduleUnit.Seconds);

        // Assert
        result.ShouldBe(new[] { 30 });
    }

    [Fact]
    public void GetValidValues_Minutes_ReturnsDivisorsOf60()
    {
        // Act
        var result = ScheduleOptions.GetValidValues(ScheduleUnit.Minutes);

        // Assert
        result.ShouldBe(new[] { 1, 2, 3, 4, 5, 6, 10, 12, 15, 20, 30 });
    }

    [Fact]
    public void GetValidValues_Hours_ReturnsDivisorsOf24()
    {
        // Act
        var result = ScheduleOptions.GetValidValues(ScheduleUnit.Hours);

        // Assert
        result.ShouldBe(new[] { 1, 2, 3, 4, 6, 8, 12 });
    }

    [Fact]
    public void IsValidValue_Seconds_30_ReturnsTrue()
    {
        // Act
        var result = ScheduleOptions.IsValidValue(ScheduleUnit.Seconds, 30);

        // Assert
        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(45)]
    [InlineData(60)]
    public void IsValidValue_Seconds_InvalidValues_ReturnsFalse(int value)
    {
        // Act
        var result = ScheduleOptions.IsValidValue(ScheduleUnit.Seconds, value);

        // Assert
        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(11)]
    [InlineData(45)]
    [InlineData(60)]
    public void IsValidValue_Minutes_InvalidValues_ReturnsFalse(int value)
    {
        // 7 doesn't divide 60 evenly, and other values are not in the valid list
        // Act
        var result = ScheduleOptions.IsValidValue(ScheduleUnit.Minutes, value);

        // Assert
        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(24)]
    public void IsValidValue_Hours_InvalidValues_ReturnsFalse(int value)
    {
        // These don't divide 24 evenly or aren't in valid list
        // Act
        var result = ScheduleOptions.IsValidValue(ScheduleUnit.Hours, value);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void GetValidValues_InvalidUnit_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var invalidUnit = (ScheduleUnit)999;

        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() => ScheduleOptions.GetValidValues(invalidUnit));
    }

    [Fact]
    public void IsValidValue_InvalidUnit_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var invalidUnit = (ScheduleUnit)999;

        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() => ScheduleOptions.IsValidValue(invalidUnit, 1));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(10)]
    [InlineData(12)]
    [InlineData(15)]
    [InlineData(20)]
    [InlineData(30)]
    public void IsValidValue_Minutes_AllValidValues_ReturnsTrue(int value)
    {
        // Act
        var result = ScheduleOptions.IsValidValue(ScheduleUnit.Minutes, value);

        // Assert
        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(12)]
    public void IsValidValue_Hours_AllValidValues_ReturnsTrue(int value)
    {
        // Act
        var result = ScheduleOptions.IsValidValue(ScheduleUnit.Hours, value);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ValidSecondValues_ContainsOnly30()
    {
        // Assert
        ScheduleOptions.ValidSecondValues.Length.ShouldBe(1);
        ScheduleOptions.ValidSecondValues.ShouldContain(30);
    }

    [Fact]
    public void ValidMinuteValues_AllDivide60Evenly()
    {
        // Assert - all valid minute values should divide 60 evenly
        foreach (var value in ScheduleOptions.ValidMinuteValues)
        {
            (60 % value).ShouldBe(0, $"Value {value} does not divide 60 evenly");
        }
    }

    [Fact]
    public void ValidHourValues_AllDivide24Evenly()
    {
        // Assert - all valid hour values should divide 24 evenly
        foreach (var value in ScheduleOptions.ValidHourValues)
        {
            (24 % value).ShouldBe(0, $"Value {value} does not divide 24 evenly");
        }
    }
}
